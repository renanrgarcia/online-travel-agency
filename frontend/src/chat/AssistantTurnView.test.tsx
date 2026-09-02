import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'

import { AssistantTurnView } from './AssistantTurnView'
import { LanguageProvider } from '../i18n/LanguageProvider'
import { STRINGS } from '../i18n/strings'
import { makeRankedOffer } from '../test/fixtures'
import type { SupplierResult, SupplierStatus } from '../api/contract'
import type { AssistantTurn } from './types'

/** One test per eval in docs/features/02-frontend/tasks/06-degraded-states.md. */

const strings = STRINGS.en

function baseTurn(overrides: Partial<AssistantTurn> = {}): AssistantTurn {
  return {
    id: 'assistant-0',
    role: 'assistant',
    status: 'streaming',
    stages: { supplierResults: [] },
    ...overrides,
  }
}

function supplierResult(overrides: Partial<SupplierResult> = {}): SupplierResult {
  return { supplierName: 'GDS', status: 'Succeeded', offerCount: 2, reason: null, ...overrides }
}

describe('AssistantTurnView — degraded states (F06)', () => {
  it('E1 — a failed supplier is reported but offers still render normally, not as an error state', () => {
    const turn = baseTurn({
      stages: {
        supplierResults: [
          supplierResult({ supplierName: 'GDS', status: 'Succeeded' }),
          supplierResult({ supplierName: 'LCC', status: 'TimedOut', offerCount: 0, reason: 'exceeded 5s timeout' }),
        ],
        rankedOffers: [makeRankedOffer()],
      },
    })
    const { container } = render(<AssistantTurnView turn={turn} />, { wrapper: LanguageProvider })

    expect(screen.getByText(strings.supplierStatusTimedOut)).toBeInTheDocument()
    // Visible, but not styled as an alarming failure -- that treatment is reserved for turn-level errors.
    expect(container.querySelector('.turn__failure')).not.toBeInTheDocument()
    expect(container.querySelector('[role="alert"]')).not.toBeInTheDocument()
    // The offer that did come back is still fully rendered.
    expect(container.querySelector('.offer-card')).not.toBeNull()
  })

  it('E2 — every supplier status renders its own distinguishable label', () => {
    const statuses: SupplierStatus[] = [
      'Succeeded',
      'PartialSuccess',
      'Failed',
      'TimedOut',
      'Cancelled',
      'SkippedCircuitOpen',
      'SkippedBudgetExhausted',
    ]
    const turn = baseTurn({
      stages: {
        supplierResults: statuses.map((status, i) => supplierResult({ supplierName: `S${i}`, status })),
      },
    })
    render(<AssistantTurnView turn={turn} />, { wrapper: LanguageProvider })

    const labels = [
      strings.supplierStatusSucceeded,
      strings.supplierStatusPartialSuccess,
      strings.supplierStatusFailed,
      strings.supplierStatusTimedOut,
      strings.supplierStatusCancelled,
      strings.supplierStatusSkippedCircuitOpen,
      strings.supplierStatusSkippedBudgetExhausted,
    ]
    // "Timed out" and "we didn't call them" are different facts -- no two statuses share a label.
    expect(new Set(labels).size).toBe(labels.length)
    for (const label of labels) expect(screen.getByText(label)).toBeInTheDocument()
  })

  it('E3 — an unclean explanation never renders as prose, and the offers stay fully usable', () => {
    const turn = baseTurn({
      status: 'complete',
      stages: {
        supplierResults: [],
        rankedOffers: [makeRankedOffer()],
        explanation: { text: '', raw: 'The best value is {{PRICE_LCC-002}}.', isClean: false },
      },
    })
    const { container } = render(<AssistantTurnView turn={turn} />, { wrapper: LanguageProvider })

    // The unclean text is never presented as the answer -- no prose element at all.
    expect(container.querySelector('.explanation__text')).toBeNull()
    expect(screen.getByText(strings.explanationUnavailable)).toBeInTheDocument()
    // The raw payload exists only inside the closed, explicitly-labelled debug box.
    expect(container.querySelector('.explanation__raw')?.textContent).toContain('PRICE_LCC-002')
    expect(container.querySelector('.offer-card')).not.toBeNull()
  })

  it('E4 — the unavailable message is plain language, with no internal jargon leaking through', () => {
    expect(strings.explanationUnavailable).not.toMatch(/token|guard|placeholder|regex|isClean/i)
    expect(strings.explanationUnavailable.length).toBeGreaterThan(0)
  })

  it('E5 — zero offers renders a clear "nothing found" message, distinct from still loading', () => {
    const turn = baseTurn({
      status: 'complete',
      stages: {
        parsedIntent: {
          origin: 'GRU',
          destination: 'LIS',
          departureDate: '2027-03-12',
          passengerCount: 1,
          language: 'en',
        },
        supplierResults: [
          supplierResult({ supplierName: 'GDS', status: 'Failed', offerCount: 0 }),
          supplierResult({ supplierName: 'NDC', status: 'Failed', offerCount: 0 }),
        ],
        rankedOffers: [],
      },
    })
    const { container } = render(<AssistantTurnView turn={turn} />, { wrapper: LanguageProvider })

    expect(screen.getByText(strings.noOffersFound)).toBeInTheDocument()
    expect(screen.queryByText(strings.waitingForOffers)).not.toBeInTheDocument()
    expect(container.querySelector('.offer-card')).toBeNull()
  })

  it("E6 — a failed turn shows the server's own reason as an alert, not a lingering spinner", () => {
    const turn = baseTurn({ status: 'failed', failure: { message: 'missing origin' } })
    render(<AssistantTurnView turn={turn} />, { wrapper: LanguageProvider })

    expect(screen.getByRole('alert')).toHaveTextContent('missing origin')
    expect(screen.queryByRole('status')).not.toBeInTheDocument()
  })

  it('E7 — a dropped connection keeps prior stages visible next to the stated interruption', () => {
    const turn = baseTurn({
      status: 'failed',
      failure: { message: strings.connectionLost },
      stages: {
        parsedIntent: {
          origin: 'GRU',
          destination: 'LIS',
          departureDate: '2027-03-12',
          passengerCount: 1,
          language: 'en',
        },
        supplierResults: [supplierResult()],
      },
    })
    render(<AssistantTurnView turn={turn} />, { wrapper: LanguageProvider })

    expect(screen.getByText(strings.stageUnderstood)).toBeInTheDocument()
    expect(screen.getByText(strings.stageSuppliers)).toBeInTheDocument()
    expect(screen.getByRole('alert')).toHaveTextContent(strings.connectionLost)
    expect(screen.queryByRole('status')).not.toBeInTheDocument()
  })

  it('E8 — the raw model output sits behind a closed, opt-in debug disclosure', async () => {
    const user = userEvent.setup()
    const turn = baseTurn({
      status: 'complete',
      stages: {
        supplierResults: [],
        rankedOffers: [makeRankedOffer()],
        explanation: { text: 'The best value is $590.00.', raw: 'The best value is {{PRICE_LCC-002}}.', isClean: true },
      },
    })
    render(<AssistantTurnView turn={turn} />, { wrapper: LanguageProvider })

    const details = screen.getByText(strings.explanationShowRaw).closest('details')
    expect(details).not.toBeNull()
    expect(details).not.toHaveAttribute('open')

    await user.click(screen.getByText(strings.explanationShowRaw))

    expect(details).toHaveAttribute('open')
    expect(screen.getByText(/PRICE_LCC-002/)).toBeInTheDocument()
  })
})

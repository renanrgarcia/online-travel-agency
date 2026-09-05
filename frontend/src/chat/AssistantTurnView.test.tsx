import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'

import { AssistantTurnView } from './AssistantTurnView'
import { LanguageProvider } from '../i18n/LanguageProvider'
import { STRINGS } from '../i18n/strings'
import { makeRankedOffer } from '../test/fixtures'
import type { ParsedIntent, SupplierResult, SupplierStatus } from '../api/contract'
import type { AssistantTurn } from './types'

/**
 * One test per eval in docs/features/02-frontend/tasks/06-degraded-states.md,
 * docs/features/02-frontend/tasks/07-bilingual-ui.md, and
 * docs/features/02-frontend/tasks/09-verify-against-a-real-supplier.md.
 */

const strings = STRINGS.en

function parsedIntent(language: string): ParsedIntent {
  return { origin: 'GRU', destination: 'LIS', departureDate: '2027-03-12', passengerCount: 1, language }
}

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

  it('E6b — a failed turn offers a new search recovery action', async () => {
    const resetConversation = vi.fn()
    const turn = baseTurn({ status: 'failed', failure: { message: strings.connectionLost } })
    render(<AssistantTurnView turn={turn} onResetConversation={resetConversation} />, {
      wrapper: LanguageProvider,
    })

    await userEvent.click(screen.getByRole('button', { name: strings.newSearch }))

    expect(resetConversation).toHaveBeenCalledOnce()
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

describe('AssistantTurnView — bilingual UI (F07)', () => {
  it('E1/E2 — a turn renders in the language its own parsed-intent reports, not the ambient chrome default', () => {
    const turn = baseTurn({
      stages: { supplierResults: [], parsedIntent: parsedIntent('pt-BR'), rankedOffers: [] },
    })
    // Ambient chrome is still English -- this search's own answer isn't.
    render(<AssistantTurnView turn={turn} />, { wrapper: LanguageProvider })

    expect(screen.getByText(STRINGS['pt-BR'].stageUnderstood)).toBeInTheDocument()
    expect(screen.queryByText(strings.stageUnderstood)).not.toBeInTheDocument()
  })

  it("E3 — a turn keeps the language it was answered in even after the app's ambient chrome moves on", () => {
    const turn = baseTurn({
      stages: { supplierResults: [], parsedIntent: parsedIntent('en'), rankedOffers: [] },
    })
    // Simulates a later, Portuguese search having already moved chrome on -- this turn must not follow.
    render(<AssistantTurnView turn={turn} />, {
      wrapper: ({ children }) => <LanguageProvider initialLanguage="pt-BR">{children}</LanguageProvider>,
    })

    expect(screen.getByText(strings.stageUnderstood)).toBeInTheDocument()
    expect(screen.queryByText(STRINGS['pt-BR'].stageUnderstood)).not.toBeInTheDocument()
  })

  it('E3 — offer cards and the comparison table nested in a turn stay frozen to that turn\'s language too', () => {
    const turn = baseTurn({
      stages: {
        supplierResults: [],
        parsedIntent: parsedIntent('en'),
        rankedOffers: [makeRankedOffer({ rank: 1, offerId: 'A', refundable: true }), makeRankedOffer({ rank: 2, offerId: 'B' })],
      },
    })
    render(<AssistantTurnView turn={turn} />, {
      wrapper: ({ children }) => <LanguageProvider initialLanguage="pt-BR">{children}</LanguageProvider>,
    })

    // OfferCard and OfferComparison read language from ambient context internally -- only the
    // LanguageOverride wrapping them inside AssistantTurnView keeps this in English. Both the card
    // and the comparison table render "Refundable", so there are two matches, not one.
    expect(screen.getAllByText(strings.offerRefundable).length).toBeGreaterThan(0)
    expect(screen.queryByText(STRINGS['pt-BR'].offerRefundable)).not.toBeInTheDocument()
    expect(screen.getByText(strings.comparisonTitle)).toBeInTheDocument()
  })

  it('E5 — supplier offer counts are translated, not an inline English literal', () => {
    const turn = baseTurn({
      stages: {
        supplierResults: [supplierResult({ offerCount: 3 })],
        parsedIntent: parsedIntent('pt-BR'),
      },
    })
    render(<AssistantTurnView turn={turn} />, { wrapper: LanguageProvider })

    expect(screen.getByText(STRINGS['pt-BR'].offerCountMany.replace('{n}', '3'))).toBeInTheDocument()
    expect(screen.queryByText('3 offers')).not.toBeInTheDocument()
  })
})

/**
 * F09's own claim is that a fourth, real supplier needs zero new frontend code -- F06's degraded-state
 * handling and F04's offer cards are already generic over `supplierName`/`SupplierStatus`/`Offer`, not
 * hardcoded to the three mocks. Backend task 25 (the real Duffel connector these evals are meant to run
 * against) doesn't exist yet, so this locks the claim down now with realistic Duffel-shaped data --
 * a name the mocks never used, a currency not in the frontend's own symbol table, and an offer-id
 * format nothing like the mocks' `LCC-002` style -- so the same tests can be pointed at the real
 * connector's actual payloads later with no rewrite, just a fixture swap.
 */
describe('AssistantTurnView — verified against a real supplier (F09)', () => {
  it('E1 — a fourth, real supplier name renders with the exact same list styling as the three mocks', () => {
    const turn = baseTurn({
      stages: {
        supplierResults: [
          supplierResult({ supplierName: 'GDS' }),
          supplierResult({ supplierName: 'NDC' }),
          supplierResult({ supplierName: 'LCC' }),
          supplierResult({ supplierName: 'Duffel' }),
        ],
      },
    })
    const { container } = render(<AssistantTurnView turn={turn} />, { wrapper: LanguageProvider })

    const names = Array.from(container.querySelectorAll('.supplier-list__name')).map((n) => n.textContent)
    expect(names).toEqual(['GDS', 'NDC', 'LCC', 'Duffel'])
    // Same category class for all four -- nothing branches on the specific name.
    container.querySelectorAll('.supplier-list__item').forEach((item) => {
      expect(item.className).toMatch(/supplier-list__item--ok/)
    })
  })

  it('E2 — Duffel timing out renders exactly like any other timed-out supplier', () => {
    const turn = baseTurn({
      stages: {
        supplierResults: [
          supplierResult({ supplierName: 'GDS' }),
          supplierResult({
            supplierName: 'Duffel',
            status: 'TimedOut',
            offerCount: 0,
            reason: 'request exceeded the configured timeout',
          }),
        ],
      },
    })
    render(<AssistantTurnView turn={turn} />, { wrapper: LanguageProvider })

    expect(screen.getByText(strings.supplierStatusTimedOut)).toBeInTheDocument()
    const duffelItem = screen.getByText('Duffel').closest('.supplier-list__item')
    expect(duffelItem?.className).toMatch(/supplier-list__item--warn/)
  })

  it('E3 — a failed Duffel connector renders like any other failed supplier, and the other offers stay usable', () => {
    const turn = baseTurn({
      stages: {
        supplierResults: [
          supplierResult({ supplierName: 'GDS' }),
          supplierResult({
            supplierName: 'Duffel',
            status: 'Failed',
            offerCount: 0,
            reason: 'origin could not be resolved to an IATA code',
          }),
        ],
        rankedOffers: [makeRankedOffer()],
      },
    })
    const { container } = render(<AssistantTurnView turn={turn} />, { wrapper: LanguageProvider })

    expect(screen.getByText(strings.supplierStatusFailed)).toBeInTheDocument()
    const duffelItem = screen.getByText('Duffel').closest('.supplier-list__item')
    expect(duffelItem?.className).toMatch(/supplier-list__item--warn/)
    expect(container.querySelector('.offer-card')).not.toBeNull()
  })

  it('E4 — an offer from Duffel renders in the same card layout as the mocks, including a currency and id shape the mocks never produce', () => {
    const duffelOffer = makeRankedOffer({
      rank: 1,
      offerId: 'off_00009hthhomAxpvE0yTFHK',
      price: 214.5,
      currency: 'GBP',
      durationMinutes: 95,
      stops: 0,
      refundable: false,
    })
    const mockOffer = makeRankedOffer({ rank: 2, offerId: 'LCC-002' })
    const turn = baseTurn({
      stages: { supplierResults: [], rankedOffers: [duffelOffer, mockOffer] },
    })
    const { container } = render(<AssistantTurnView turn={turn} />, { wrapper: LanguageProvider })

    const cards = container.querySelectorAll('.offer-card')
    expect(cards).toHaveLength(2)
    // The two-offer set is also within the comparison table's own top-3, so price/stops legitimately
    // repeat there too -- scope to the card itself rather than the whole document.
    const duffelCard = within(cards[0] as HTMLElement)
    // GBP has no entry in offerFormatting.ts's CURRENCY_SYMBOLS -- this is the existing verbatim
    // fallback (F04 E2) doing its job, not new code written for Duffel specifically.
    expect(duffelCard.getByText('214.50 GBP')).toBeInTheDocument()
    expect(duffelCard.getByText('off_00009hthhomAxpvE0yTFHK')).toBeInTheDocument()
    expect(duffelCard.getByText('nonstop')).toBeInTheDocument()
  })
})

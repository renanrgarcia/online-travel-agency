import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'

import { BookingTurnView } from './BookingTurnView'
import { LanguageProvider } from '../i18n/LanguageProvider'
import { STRINGS } from '../i18n/strings'
import { makeRankedOffer } from '../test/fixtures'
import type { BookingTurn } from './types'

const strings = STRINGS.en

function renderTurn(turn: BookingTurn) {
  const onConfirm = vi.fn()
  const onCancel = vi.fn()
  render(
    <LanguageProvider>
      <BookingTurnView turn={turn} onConfirm={onConfirm} onCancel={onCancel} />
    </LanguageProvider>,
  )
  return { onConfirm, onCancel }
}

function baseTurn(overrides: Partial<BookingTurn> = {}): BookingTurn {
  return {
    id: 'booking-0',
    role: 'booking',
    bookingId: 'b-0',
    offer: makeRankedOffer(),
    language: 'en',
    status: 'collecting-details',
    ...overrides,
  }
}

describe('BookingTurnView', () => {
  it('collecting-details: asks for an email and confirms with it, the turn id, and the bookingId', async () => {
    const user = userEvent.setup()
    const { onConfirm } = renderTurn(baseTurn())

    await user.type(screen.getByRole('textbox', { name: strings.bookingTravellerEmailLabel }), 't@example.com')
    await user.click(screen.getByRole('button', { name: strings.bookingConfirm }))

    expect(onConfirm).toHaveBeenCalledWith('booking-0', 'b-0', expect.objectContaining({ offerId: 'LCC-002' }), 't@example.com')
  })

  it('collecting-details: rejects an empty email inline rather than confirming', async () => {
    const user = userEvent.setup()
    const { onConfirm } = renderTurn(baseTurn())

    await user.click(screen.getByRole('button', { name: strings.bookingConfirm }))

    expect(screen.getByRole('alert')).toHaveTextContent(strings.bookingEmailRequired)
    expect(onConfirm).not.toHaveBeenCalled()
  })

  it('collecting-details: cancelling calls onCancel with the turn id', async () => {
    const user = userEvent.setup()
    const { onCancel } = renderTurn(baseTurn())

    await user.click(screen.getByRole('button', { name: strings.bookingCancel }))

    expect(onCancel).toHaveBeenCalledWith('booking-0')
  })

  it('polling: shows the step matching customStatus, translated', () => {
    renderTurn(baseTurn({ status: 'polling', customStatus: { step: 'issuing-ticket' } }))

    expect(screen.getByRole('status')).toHaveTextContent(strings.bookingStepIssuingTicket)
  })

  it('booked: shows the ticket number', () => {
    renderTurn(
      baseTurn({
        status: 'booked',
        output: {
          Success: true,
          AuthorizationId: 'AUTH-1',
          OrderId: 'ORD-1',
          TicketNumber: 'TKT-ORD-1',
          FailedStage: null,
          FailureReason: null,
        },
      }),
    )

    expect(screen.getByText(strings.bookingBookedTitle)).toBeInTheDocument()
    expect(screen.getByText('TKT-ORD-1')).toBeInTheDocument()
  })

  it('saga-failed + compensated: states the rollback explicitly, in plain language', () => {
    renderTurn(
      baseTurn({
        status: 'saga-failed',
        customStatus: { step: 'failed', stage: 'IssueTicket', compensated: true },
        output: {
          Success: false,
          AuthorizationId: 'AUTH-1',
          OrderId: 'ORD-1',
          TicketNumber: null,
          FailedStage: 'IssueTicket',
          FailureReason: "Ticket issuance failed for offer 'X'.",
        },
      }),
    )

    expect(screen.getByText(strings.bookingFailedTitle)).toBeInTheDocument()
    expect(screen.getByText("Ticket issuance failed for offer 'X'.")).toBeInTheDocument()
    // The user-visible proof nothing was silently charged -- the whole point of F05 E3.
    expect(screen.getByText(strings.bookingCompensated)).toBeInTheDocument()
  })

  it('saga-failed + compensation itself failed: says so loudly, distinct from a normal rollback', () => {
    renderTurn(
      baseTurn({
        status: 'saga-failed',
        customStatus: {
          step: 'failed',
          stage: 'IssueTicket',
          compensated: false,
          warning: 'compensation failed: VoidPayment - some reason',
        },
        output: {
          Success: false,
          AuthorizationId: 'AUTH-1',
          OrderId: 'ORD-1',
          TicketNumber: null,
          FailedStage: 'IssueTicket',
          FailureReason: 'Ticket issuance failed.',
        },
      }),
    )

    expect(screen.getByText(strings.bookingCompensationFailed)).toBeInTheDocument()
    expect(screen.queryByText(strings.bookingCompensated)).not.toBeInTheDocument()
  })

  it('saga-failed at the first step: nothing to compensate, states that plainly', () => {
    renderTurn(
      baseTurn({
        status: 'saga-failed',
        customStatus: { step: 'failed', stage: 'AuthorizePayment' },
        output: {
          Success: false,
          AuthorizationId: null,
          OrderId: null,
          TicketNumber: null,
          FailedStage: 'AuthorizePayment',
          FailureReason: 'Payment authorization failed.',
        },
      }),
    )

    expect(screen.getByText(strings.bookingNotCompensated)).toBeInTheDocument()
  })

  it('error: an unknown bookingId renders the not-found message, not a raw code', () => {
    renderTurn(baseTurn({ status: 'error', error: { message: 'not-found' } }))

    expect(screen.getByText(strings.bookingErrorNotFound)).toBeInTheDocument()
  })

  it('error: a network failure renders the network message', () => {
    renderTurn(baseTurn({ status: 'error', error: { message: 'network' } }))

    expect(screen.getByText(strings.bookingErrorNetwork)).toBeInTheDocument()
  })

  it('error: a server-provided message (e.g. expired assertion) is shown verbatim', () => {
    renderTurn(baseTurn({ status: 'error', error: { message: 'The price assertion has expired.' } }))

    expect(screen.getByText('The price assertion has expired.')).toBeInTheDocument()
  })

  it('F07 E3 — renders in the language it was booked in, not the app\'s current ambient chrome language', () => {
    const turn = baseTurn({ language: 'pt-BR', status: 'collecting-details' })
    render(
      // The ambient chrome has since moved to English, e.g. from a later English search.
      <LanguageProvider initialLanguage="en">
        <BookingTurnView turn={turn} onConfirm={vi.fn()} onCancel={vi.fn()} />
      </LanguageProvider>,
    )

    const ptStrings = STRINGS['pt-BR']
    expect(screen.getByRole('button', { name: ptStrings.bookingConfirm })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: strings.bookingConfirm })).not.toBeInTheDocument()
  })
})

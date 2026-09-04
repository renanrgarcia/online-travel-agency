import { act, renderHook } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { useBookingFlow } from './useBookingFlow'
import { useChat } from './useChat'
import type { BookingTurn } from './types'
import { makeRankedOffer } from '../test/fixtures'
import type { CreateBookingRequest } from '../api/bookingContract'

/** One test per eval in docs/features/02-frontend/tasks/05-the-booking-turn.md. */

vi.mock('../config', () => ({
  getApiBaseUrl: () => '',
  getBookingApiBaseUrl: () => '',
}))

// The poll loop uses a real setTimeout interval; fake timers let each test advance it deterministically
// instead of actually waiting 1.5s of wall-clock time per poll.
beforeEach(() => {
  vi.useFakeTimers()
})
afterEach(() => {
  vi.useRealTimers()
})

/** Advances fake time by `ms` inside `act`, letting any promises chained off a resolved fetch (and
 * any newly-scheduled poll timer) settle before the next assertion. `vi.waitFor` doesn't cooperate
 * with fake timers here, so each async step in a test is a deliberate, explicit flush instead. */
async function flush(ms = 0) {
  await act(async () => {
    await vi.advanceTimersByTimeAsync(ms)
  })
}

function jsonResponse(body: unknown, status: number): Response {
  return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } })
}

function bookingStatusBody(overrides: {
  runtimeStatus?: string
  customStatus?: object
  output?: object | null
}) {
  return {
    bookingId: 'the-booking-id',
    runtimeStatus: overrides.runtimeStatus ?? 'Running',
    customStatus: overrides.customStatus ? JSON.stringify(overrides.customStatus) : null,
    output: overrides.output === null ? null : JSON.stringify(overrides.output ?? {}),
    createdAt: '2027-01-01T00:00:00Z',
    lastUpdatedAt: '2027-01-01T00:00:01Z',
  }
}

/** A fake `fetch` driven by a queue: each call shifts the next configured response off the front,
 * so a test can script exactly what the POST and each subsequent poll return, in order. */
function fakeFetch() {
  const queue: Response[] = []
  const calls: { url: string; init?: RequestInit }[] = []
  const fetchImpl: typeof fetch = vi.fn(async (input, init) => {
    calls.push({ url: String(input), init })
    const next = queue.shift()
    if (!next) throw new Error('fakeFetch: no more responses queued')
    return next
  })
  return { fetchImpl, calls, push: (response: Response) => queue.push(response) }
}

function bookingTurnOf(turns: readonly { id: string }[]): BookingTurn {
  const turn = turns.find((t): t is BookingTurn => 'role' in t && t.role === 'booking')
  if (!turn) throw new Error('expected a booking turn to exist')
  return turn
}

function setup() {
  const fake = fakeFetch()
  const { result } = renderHook(() => {
    const chat = useChat()
    const booking = useBookingFlow(chat, { fetchImpl: fake.fetchImpl })
    return { chat, booking }
  })
  return { result, ...fake }
}

describe('useBookingFlow', () => {
  it('E1 — a normal booking: 202, progress renders, terminal state shows the ticket number', async () => {
    const { result, push, calls } = setup()
    const offer = makeRankedOffer()

    let turnId = ''
    act(() => {
      turnId = result.current.booking.startBooking(offer, 'en')
    })
    const bookingId = bookingTurnOf(result.current.chat.turns).bookingId
    expect(bookingTurnOf(result.current.chat.turns).status).toBe('collecting-details')

    push(jsonResponse({ Id: bookingId }, 202))
    push(jsonResponse(bookingStatusBody({ runtimeStatus: 'Running' }), 200))
    push(
      jsonResponse(
        bookingStatusBody({
          runtimeStatus: 'Completed',
          output: { Success: true, AuthorizationId: 'AUTH-1', OrderId: 'ORD-1', TicketNumber: 'TKT-1' },
        }),
        200,
      ),
    )

    act(() => {
      result.current.booking.confirmBooking(turnId, bookingId, offer, 't@example.com')
    })
    await flush() // POST resolves -> first poll fires
    expect(bookingTurnOf(result.current.chat.turns).status).toBe('polling')

    await flush(2000) // second poll, still Running
    await flush(2000) // third poll, now Completed

    const finalTurn = bookingTurnOf(result.current.chat.turns)
    expect(finalTurn.status).toBe('booked')
    expect(finalTurn.output?.TicketNumber).toBe('TKT-1')

    // The POST carried the offer's own price assertion, not a client-chosen amount (E8).
    const postCall = calls.find((c) => c.init?.method === 'POST')!
    const body = JSON.parse(postCall.init!.body as string) as CreateBookingRequest
    expect(body.priceAssertion).toEqual(offer.priceAssertion)
    expect(body.bookingId).toBe(bookingId)
  })

  it('E2 — progress reflects customStatus through each saga step', async () => {
    const { result, push } = setup()
    const offer = makeRankedOffer()
    let turnId = ''
    act(() => {
      turnId = result.current.booking.startBooking(offer, 'en')
    })
    const bookingId = bookingTurnOf(result.current.chat.turns).bookingId

    push(jsonResponse({ Id: bookingId }, 202))
    push(jsonResponse(bookingStatusBody({ runtimeStatus: 'Running', customStatus: { step: 'authorizing-payment' } }), 200))

    act(() => {
      result.current.booking.confirmBooking(turnId, bookingId, offer, 't@example.com')
    })
    await flush()
    expect(bookingTurnOf(result.current.chat.turns).customStatus?.step).toBe('authorizing-payment')

    push(jsonResponse(bookingStatusBody({ runtimeStatus: 'Running', customStatus: { step: 'issuing-ticket' } }), 200))
    await flush(2000)
    expect(bookingTurnOf(result.current.chat.turns).customStatus?.step).toBe('issuing-ticket')
  })

  it('E3 — a FAIL-TICKET offer shows failure and states the rollback explicitly', async () => {
    const { result, push } = setup()
    const offer = makeRankedOffer({ offerId: 'NDC-FAIL-TICKET-xyz' })
    let turnId = ''
    act(() => {
      turnId = result.current.booking.startBooking(offer, 'en')
    })
    const bookingId = bookingTurnOf(result.current.chat.turns).bookingId

    push(jsonResponse({ Id: bookingId }, 202))
    push(
      jsonResponse(
        bookingStatusBody({
          runtimeStatus: 'Completed',
          customStatus: { step: 'failed', stage: 'IssueTicket', compensated: true },
          output: {
            Success: false,
            AuthorizationId: 'AUTH-1',
            OrderId: 'ORD-1',
            TicketNumber: null,
            FailedStage: 'IssueTicket',
            FailureReason: "Ticket issuance failed for offer 'NDC-FAIL-TICKET-xyz'.",
          },
        }),
        200,
      ),
    )

    act(() => {
      result.current.booking.confirmBooking(turnId, bookingId, offer, 't@example.com')
    })
    await flush()

    const turn = bookingTurnOf(result.current.chat.turns)
    expect(turn.status).toBe('saga-failed')
    expect(turn.output?.FailedStage).toBe('IssueTicket')
    expect(turn.customStatus?.compensated).toBe(true)
  })

  it('E4 — confirming does not regenerate bookingId, and the same id is sent every time', async () => {
    const { result, push, calls } = setup()
    const offer = makeRankedOffer()
    let turnId = ''
    act(() => {
      turnId = result.current.booking.startBooking(offer, 'en')
    })
    const bookingId = bookingTurnOf(result.current.chat.turns).bookingId

    push(jsonResponse({ Id: bookingId }, 202))
    push(jsonResponse(bookingStatusBody({ runtimeStatus: 'Running' }), 200))

    act(() => {
      result.current.booking.confirmBooking(turnId, bookingId, offer, 't@example.com')
    })
    await flush()

    expect(bookingTurnOf(result.current.chat.turns).bookingId).toBe(bookingId)
    const postBody = JSON.parse(calls[0]!.init!.body as string) as CreateBookingRequest
    expect(postBody.bookingId).toBe(bookingId)
  })

  it('E5 — FailedStage and FailureReason are both exposed on the turn in a saga failure', async () => {
    const { result, push } = setup()
    const offer = makeRankedOffer({ offerId: 'NDC-FAIL-ORDER-xyz' })
    let turnId = ''
    act(() => {
      turnId = result.current.booking.startBooking(offer, 'en')
    })
    const bookingId = bookingTurnOf(result.current.chat.turns).bookingId

    push(jsonResponse({ Id: bookingId }, 202))
    push(
      jsonResponse(
        bookingStatusBody({
          runtimeStatus: 'Completed',
          customStatus: { step: 'failed', stage: 'CreateOrder', compensated: true },
          output: {
            Success: false,
            AuthorizationId: 'AUTH-1',
            OrderId: null,
            TicketNumber: null,
            FailedStage: 'CreateOrder',
            FailureReason: "Order creation failed for offer 'NDC-FAIL-ORDER-xyz'.",
          },
        }),
        200,
      ),
    )

    act(() => {
      result.current.booking.confirmBooking(turnId, bookingId, offer, 't@example.com')
    })
    await flush()

    const turn = bookingTurnOf(result.current.chat.turns)
    expect(turn.status).toBe('saga-failed')
    expect(turn.output?.FailedStage).toBe('CreateOrder')
    expect(turn.output?.FailureReason).toContain('Order creation failed')
  })

  it('E6 — polling stops at a terminal state rather than continuing forever', async () => {
    const { result, push, calls } = setup()
    const offer = makeRankedOffer()
    let turnId = ''
    act(() => {
      turnId = result.current.booking.startBooking(offer, 'en')
    })
    const bookingId = bookingTurnOf(result.current.chat.turns).bookingId

    push(jsonResponse({ Id: bookingId }, 202))
    push(
      jsonResponse(
        bookingStatusBody({ runtimeStatus: 'Completed', output: { Success: true, TicketNumber: 'TKT-1' } }),
        200,
      ),
    )

    act(() => {
      result.current.booking.confirmBooking(turnId, bookingId, offer, 't@example.com')
    })
    await flush()
    expect(bookingTurnOf(result.current.chat.turns).status).toBe('booked')

    const callCountAtTerminal = calls.length
    await flush(10_000)
    // No further requests were made after the terminal state -- an orphaned timer would show up here.
    expect(calls.length).toBe(callCountAtTerminal)
  })

  it('E7 — an unknown bookingId renders a defined not-found state, not a crash', async () => {
    const { result, push } = setup()
    const offer = makeRankedOffer()
    let turnId = ''
    act(() => {
      turnId = result.current.booking.startBooking(offer, 'en')
    })
    const bookingId = bookingTurnOf(result.current.chat.turns).bookingId

    push(jsonResponse({ Id: bookingId }, 202))
    push(new Response(JSON.stringify({ error: 'booking not found', bookingId }), { status: 404 }))

    act(() => {
      result.current.booking.confirmBooking(turnId, bookingId, offer, 't@example.com')
    })
    await flush()

    const turn = bookingTurnOf(result.current.chat.turns)
    expect(turn.status).toBe('error')
    expect(turn.error?.message).toBe('not-found')
  })

  it('E8 — the request always carries the offer price assertion, never a client-invented amount', async () => {
    const { result, push, calls } = setup()
    const offer = makeRankedOffer({ price: 730, currency: 'USD' })
    let turnId = ''
    act(() => {
      turnId = result.current.booking.startBooking(offer, 'en')
    })
    const bookingId = bookingTurnOf(result.current.chat.turns).bookingId

    push(jsonResponse({ Id: bookingId }, 202))
    push(jsonResponse(bookingStatusBody({ runtimeStatus: 'Running' }), 200))

    act(() => {
      result.current.booking.confirmBooking(turnId, bookingId, offer, 't@example.com')
    })
    await flush()

    const body = JSON.parse(calls[0]!.init!.body as string) as CreateBookingRequest
    expect(body.priceAssertion.amount).toBe(offer.priceAssertion.amount)
    expect(body.priceAssertion.signature).toBe(offer.priceAssertion.signature)
  })

  it('reports a rejected POST (e.g. an invalid price assertion) as a defined error, not a crash', async () => {
    const { result, push } = setup()
    const offer = makeRankedOffer()
    let turnId = ''
    act(() => {
      turnId = result.current.booking.startBooking(offer, 'en')
    })
    const bookingId = bookingTurnOf(result.current.chat.turns).bookingId

    push(jsonResponse({ error: 'The price assertion has expired.', reason: 'price_assertion_expired' }, 400))

    act(() => {
      result.current.booking.confirmBooking(turnId, bookingId, offer, 't@example.com')
    })
    await flush()

    const turn = bookingTurnOf(result.current.chat.turns)
    expect(turn.status).toBe('error')
    expect(turn.error?.message).toBe('The price assertion has expired.')
  })
})

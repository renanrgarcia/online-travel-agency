import { useCallback, useEffect, useRef } from 'react'

import { createBooking, getBookingStatus, isTerminalRuntimeStatus, type FetchLike } from '../api/bookingApi'
import type { RankedOffer } from '../api/contract'
import { getBookingApiBaseUrl } from '../config'
import type { ChatController } from './useChat'

const POLL_INTERVAL_MS = 1500

/** Mocked -- real payment collection is explicitly out of scope (F05); the saga only ever sees this
 * fixed token, matching the demo curl examples in docs/reference/07-booking-saga.md. */
const MOCK_PAYMENT_METHOD_TOKEN = 'tok_test'

export interface BookingFlow {
  /** Creates the booking turn (F02-style: an action becomes a new turn), before any network call. */
  startBooking: (offer: RankedOffer) => string
  /** Submits the booking and starts polling. Takes `bookingId` and `offer` explicitly rather than
   * reading them back off `chat.turns` -- state read through a stale closure in a recursive poll is
   * exactly the bug class worth designing away, not catching later. */
  confirmBooking: (turnId: string, bookingId: string, offer: RankedOffer, travellerEmail: string) => void
}

export interface UseBookingFlowOptions {
  fetchImpl?: FetchLike
}

/**
 * Joins the chat state ({@link useChat}) to the booking saga's HTTP contract — polling, not a stream
 * (F05's locked decision), so this hook's shape is deliberately different from F03's
 * `useSearchChat`: no persistent connection to close, but a poll loop to stop instead.
 */
export function useBookingFlow(chat: ChatController, options: UseBookingFlowOptions = {}): BookingFlow {
  const { fetchImpl } = options
  const { startBooking: createBookingTurn, updateBooking } = chat

  // One poll timer per booking turn, so multiple bookings in one conversation poll independently.
  const pollTimers = useRef(new Map<string, ReturnType<typeof setTimeout>>());

  const stopPolling = useCallback((turnId: string) => {
    const timer = pollTimers.current.get(turnId)
    if (timer !== undefined) {
      clearTimeout(timer)
      pollTimers.current.delete(turnId)
    }
  }, [])

  // E6: an abandoned booking stops polling -- an orphaned interval is a battery and quota drain
  // nothing surfaces, the same reasoning F03 applies to closing an abandoned search stream.
  useEffect(() => {
    const timers = pollTimers.current
    return () => {
      for (const timer of timers.values()) clearTimeout(timer)
      timers.clear()
    }
  }, [])

  const poll = useCallback(
    async (turnId: string, bookingId: string) => {
      const result = await getBookingStatus(bookingId, getBookingApiBaseUrl(), fetchImpl)

      if (!result.ok) {
        stopPolling(turnId)
        updateBooking(turnId, (turn) => ({
          ...turn,
          status: 'error',
          error: { message: result.notFound ? 'not-found' : result.error },
        }))
        return
      }

      const terminal = isTerminalRuntimeStatus(result.status.runtimeStatus)
      updateBooking(turnId, (turn) => ({
        ...turn,
        status: terminal ? (result.output?.Success ? 'booked' : result.output ? 'saga-failed' : 'error') : 'polling',
        customStatus: result.customStatus ?? turn.customStatus,
        output: result.output ?? turn.output,
        error: terminal && !result.output ? { message: 'malformed-output' } : turn.error,
      }))

      if (terminal) {
        stopPolling(turnId)
        return
      }

      pollTimers.current.set(
        turnId,
        setTimeout(() => void poll(turnId, bookingId), POLL_INTERVAL_MS),
      )
    },
    [fetchImpl, stopPolling, updateBooking],
  )

  const confirmBooking = useCallback(
    (turnId: string, bookingId: string, offer: RankedOffer, travellerEmail: string) => {
      updateBooking(turnId, (turn) => ({ ...turn, status: 'submitting' }))

      void (async () => {
        const result = await createBooking(
          {
            bookingId,
            offerId: offer.offerId,
            travellerEmail,
            amount: offer.price,
            currency: offer.currency,
            paymentMethodToken: MOCK_PAYMENT_METHOD_TOKEN,
            priceAssertion: offer.priceAssertion,
          },
          getBookingApiBaseUrl(),
          fetchImpl,
        )

        if (!result.ok) {
          updateBooking(turnId, (turn) => ({ ...turn, status: 'error', error: { message: result.error } }))
          return
        }

        updateBooking(turnId, (turn) => ({ ...turn, status: 'polling' }))
        void poll(turnId, bookingId)
      })()
    },
    [fetchImpl, poll, updateBooking],
  )

  return { startBooking: createBookingTurn, confirmBooking }
}

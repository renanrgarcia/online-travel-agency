import { useCallback, useRef, useState } from 'react'

import { assertNeverEvent, type RankedOffer, type SearchStreamEvent } from '../api/contract'
import type { Language } from '../i18n/strings'
import { emptyStages, type AssistantTurn, type BookingTurn, type Turn } from './types'

/**
 * Owns the conversation — every turn, search and booking alike. Deliberately knows nothing about the
 * network: task F03 drives the search half by calling {@link ChatController.applyEvent} as SSE events
 * arrive, task F05 drives the booking half by calling {@link ChatController.updateBooking}, and the
 * F02 tests drive the same methods by hand. That keeps every transport out of the components entirely.
 */
export interface ChatController {
  turns: Turn[]
  /** One in-flight search at a time — a locked decision, so this gates the composer. Bookings aren't
   * included: each is independent and doesn't block a new search or another booking. */
  isStreaming: boolean
  /** Creates the user turn and its pending assistant turn. Returns the assistant turn's id. */
  submit: (text: string) => string | undefined
  applyEvent: (turnId: string, event: SearchStreamEvent) => void
  completeTurn: (turnId: string) => void
  failTurn: (turnId: string, message: string) => void
  /** Creates a new booking turn in `collecting-details`, with a `bookingId` generated once here and
   * never regenerated for this attempt (F05 E4). `language` is the language the source assistant turn
   * was answered in (F07 E3) — frozen onto the booking turn, not read back from ambient chrome state.
   * Returns the new turn's id. */
  startBooking: (offer: RankedOffer, language: Language) => string
  updateBooking: (turnId: string, update: (turn: BookingTurn) => BookingTurn) => void
  /** Removes a turn outright — only meaningful for a booking turn still in `collecting-details`
   * (Cancel): nothing was submitted yet, so there's no server-side state to reconcile, just a local
   * dismissal. Not exposed for any other turn type. */
  removeTurn: (turnId: string) => void
}

export interface UseChatOptions {
  /** Called after the turns are created, so F03 can open the stream for this query. */
  onStart?: (query: string, turnId: string) => void
}

export function useChat(options: UseChatOptions = {}): ChatController {
  const { onStart } = options
  const [turns, setTurns] = useState<Turn[]>([])
  const [isStreaming, setIsStreaming] = useState(false)
  const nextId = useRef(0)

  const updateAssistantTurn = useCallback(
    (turnId: string, update: (turn: AssistantTurn) => AssistantTurn) => {
      setTurns((current) =>
        current.map((turn) =>
          turn.id === turnId && turn.role === 'assistant' ? update(turn) : turn,
        ),
      )
    },
    [],
  )

  const submit = useCallback(
    (text: string): string | undefined => {
      const query = text.trim()
      if (query.length === 0) return undefined

      const id = nextId.current++
      const userTurn: Turn = { id: `user-${id}`, role: 'user', text: query }
      const assistantTurn: AssistantTurn = {
        id: `assistant-${id}`,
        role: 'assistant',
        status: 'streaming',
        stages: emptyStages(),
      }

      setTurns((current) => [...current, userTurn, assistantTurn])
      setIsStreaming(true)
      onStart?.(query, assistantTurn.id)
      return assistantTurn.id
    },
    [onStart],
  )

  const applyEvent = useCallback(
    (turnId: string, event: SearchStreamEvent) => {
      updateAssistantTurn(turnId, (turn) => {
        switch (event.type) {
          case 'parsed-intent':
            return { ...turn, stages: { ...turn.stages, parsedIntent: event.data } }
          case 'supplier-result':
            return {
              ...turn,
              stages: {
                ...turn.stages,
                supplierResults: [...turn.stages.supplierResults, event.data],
              },
            }
          case 'ranked-offers':
            return { ...turn, stages: { ...turn.stages, rankedOffers: event.data } }
          case 'explanation':
            return { ...turn, stages: { ...turn.stages, explanation: event.data } }
          case 'error':
            return { ...turn, status: 'failed', failure: { message: event.data.message } }
          default:
            return assertNeverEvent(event)
        }
      })

      if (event.type === 'error') setIsStreaming(false)
    },
    [updateAssistantTurn],
  )

  const completeTurn = useCallback(
    (turnId: string) => {
      updateAssistantTurn(turnId, (turn) =>
        turn.status === 'streaming' ? { ...turn, status: 'complete' } : turn,
      )
      setIsStreaming(false)
    },
    [updateAssistantTurn],
  )

  const failTurn = useCallback(
    (turnId: string, message: string) => {
      updateAssistantTurn(turnId, (turn) => ({ ...turn, status: 'failed', failure: { message } }))
      setIsStreaming(false)
    },
    [updateAssistantTurn],
  )

  const startBooking = useCallback((offer: RankedOffer, language: Language): string => {
    const id = `booking-${nextId.current++}`
    // A real random id, not a counter -- this becomes the saga's orchestration instance id, and a
    // counter would collide across page loads / concurrent tabs in a way crypto.randomUUID won't.
    const bookingId = crypto.randomUUID()
    const bookingTurn: BookingTurn = {
      id,
      role: 'booking',
      bookingId,
      offer,
      language,
      status: 'collecting-details',
    }
    setTurns((current) => [...current, bookingTurn])
    return id
  }, [])

  const updateBooking = useCallback((turnId: string, update: (turn: BookingTurn) => BookingTurn) => {
    setTurns((current) =>
      current.map((turn) => (turn.id === turnId && turn.role === 'booking' ? update(turn) : turn)),
    )
  }, [])

  const removeTurn = useCallback((turnId: string) => {
    setTurns((current) => current.filter((turn) => turn.id !== turnId))
  }, [])

  return {
    turns,
    isStreaming,
    submit,
    applyEvent,
    completeTurn,
    failTurn,
    startBooking,
    updateBooking,
    removeTurn,
  }
}

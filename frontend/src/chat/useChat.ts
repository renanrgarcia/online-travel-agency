import { useCallback, useRef, useState } from 'react'

import { assertNeverEvent, type SearchStreamEvent } from '../api/contract'
import { emptyStages, type AssistantTurn, type Turn } from './types'

/**
 * Owns the conversation. Deliberately knows nothing about the network: task F03 drives it by calling
 * {@link ChatController.applyEvent} as SSE events arrive, and the F02 tests drive the same methods by
 * hand. That keeps the transport out of the components entirely.
 */
export interface ChatController {
  turns: Turn[]
  /** One in-flight search at a time — a locked decision, so this gates the composer. */
  isStreaming: boolean
  /** Creates the user turn and its pending assistant turn. Returns the assistant turn's id. */
  submit: (text: string) => string | undefined
  applyEvent: (turnId: string, event: SearchStreamEvent) => void
  completeTurn: (turnId: string) => void
  failTurn: (turnId: string, message: string) => void
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

  return { turns, isStreaming, submit, applyEvent, completeTurn, failTurn }
}

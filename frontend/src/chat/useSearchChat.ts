import { useCallback, useEffect, useRef } from 'react'

import { openSearchStream, type EventSourceFactory, type SearchStreamHandle } from '../api/searchStream'
import { getApiBaseUrl } from '../config'
import { useLanguage } from '../i18n/LanguageProvider'
import { useChat, type ChatController } from './useChat'

export interface UseSearchChatOptions {
  /** Test seam only — production always takes {@link openSearchStream}'s own default. */
  createEventSource?: EventSourceFactory
}

/**
 * Joins the chat state ({@link useChat}) to the real SSE stream ({@link openSearchStream}) — the
 * wiring F03 exists to add. `useChat` itself stays network-free; this is the one place that knows
 * both halves.
 */
export function useSearchChat(options: UseSearchChatOptions = {}): ChatController {
  const { createEventSource } = options
  const { strings } = useLanguage()
  const activeStream = useRef<SearchStreamHandle | null>(null)

  const chat = useChat({
    aiUnavailableMessage: strings.aiUnavailable,
    onStart: (query, turnId) => {
      // One in-flight search at a time (the composer enforces this), but guard anyway: a leftover
      // handle here would mean two streams silently racing to update the same or a stale turn.
      activeStream.current?.close()

      activeStream.current = openSearchStream(
        query,
        {
          onEvent: (event) => chat.applyEvent(turnId, event),
          onComplete: () => chat.completeTurn(turnId),
          onFailure: (failure) => {
            if (failure.kind === 'connection-lost') {
              // F06 owns real degraded-state design; this is deliberately the crude version F03's
              // scope allows for. A malformed single frame is *not* treated as failing the whole
              // turn — the stream itself is still alive per F01's own contract.
              chat.failTurn(turnId, strings.connectionLost)
            }
          },
        },
        { baseUrl: getApiBaseUrl(), createEventSource },
      )
    },
  })

  // E6: an abandoned search stops spending supplier budget — close whatever's open when this
  // component goes away, not just when a turn finishes normally.
  useEffect(() => () => activeStream.current?.close(), [])

  const resetConversation = useCallback(() => {
    activeStream.current?.close()
    activeStream.current = null
    chat.resetConversation()
  }, [chat.resetConversation])

  return { ...chat, resetConversation }
}

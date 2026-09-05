import { useCallback, useRef } from 'react'

import { getMoreOffers, type GetMoreOffersResult } from '../api/offersApi'
import type { FetchLike } from '../api/bookingApi'
import { getApiBaseUrl } from '../config'
import type { ChatController } from './useChat'
import { OFFERS_PAGE_SIZE, type AssistantTurn } from './types'

export interface ShowMoreOffers {
  showMore: (turnId: string) => void
}

export interface UseShowMoreOffersOptions {
  fetchImpl?: FetchLike
}

/**
 * Joins the chat state ({@link useChat}) to backend task 26's paginated-offers endpoint — the F10
 * equivalent of what {@link import('./useBookingFlow').useBookingFlow} is for the booking saga: a
 * request/response feature, not a stream, kept in its own hook so `useChat` stays network-free.
 */
export function useShowMoreOffers(chat: ChatController, options: UseShowMoreOffersOptions = {}): ShowMoreOffers {
  const { fetchImpl } = options
  const { turns, updateAssistantTurn } = chat

  // A ref, not just `moreOffersStatus` in state: two clicks in the same tick both read pre-update
  // state, so state alone can't stop a second in-flight request (F10 E4) -- the same reasoning
  // useBookingFlow's poll-timer ref exists for.
  const loadingTurns = useRef(new Set<string>())

  const showMore = useCallback(
    (turnId: string) => {
      if (loadingTurns.current.has(turnId)) return

      const turn = turns.find((t): t is AssistantTurn => t.id === turnId && t.role === 'assistant')
      const searchId = turn?.stages.searchId
      const offset = turn?.stages.rankedOffers?.length ?? 0
      if (!turn || !searchId) return

      loadingTurns.current.add(turnId)
      updateAssistantTurn(turnId, (t) => ({
        ...t,
        stages: { ...t.stages, moreOffersStatus: 'loading' },
      }))

      void (async () => {
        const result: GetMoreOffersResult = await getMoreOffers(
          searchId,
          offset,
          OFFERS_PAGE_SIZE,
          getApiBaseUrl(),
          fetchImpl,
        )
        loadingTurns.current.delete(turnId)

        if (!result.ok) {
          updateAssistantTurn(turnId, (t) => ({
            ...t,
            stages: { ...t.stages, moreOffersStatus: result.expired ? 'expired' : 'error' },
          }))
          return
        }

        updateAssistantTurn(turnId, (t) => ({
          ...t,
          stages: {
            ...t.stages,
            rankedOffers: [...(t.stages.rankedOffers ?? []), ...result.offers],
            moreOffersStatus: result.offers.length < OFFERS_PAGE_SIZE ? 'exhausted' : 'idle',
          },
        }))
      })()
    },
    [turns, updateAssistantTurn, fetchImpl],
  )

  return { showMore }
}

import { act, renderHook } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'

import { useChat, type ChatController } from './useChat'
import { useShowMoreOffers } from './useShowMoreOffers'
import type { AssistantTurn } from './types'
import { makeRankedOffer } from '../test/fixtures'

/** One test per eval in docs/features/02-frontend/tasks/10-show-more-offers.md. */

vi.mock('../config', () => ({ getApiBaseUrl: () => '' }))

function jsonResponse(body: unknown, status: number): Response {
  return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } })
}

/** A fake `fetch` driven by a queue, same convention as useBookingFlow.test.ts's `fakeFetch`. */
function fakeFetch() {
  const queue: Response[] = []
  const calls: { url: string }[] = []
  const fetchImpl: typeof fetch = vi.fn(async (input) => {
    calls.push({ url: String(input) })
    const next = queue.shift()
    if (!next) throw new Error('fakeFetch: no more responses queued')
    return next
  })
  return { fetchImpl, calls, push: (response: Response) => queue.push(response) }
}

/** No fake timers needed here (no poll loop, unlike booking) — a real macrotask tick is enough to
 * drain the fetch/json/setState microtask chain between a click and its assertions. */
async function flush() {
  await act(async () => {
    await new Promise((resolve) => setTimeout(resolve, 0))
  })
}

function assistantTurnOf(turns: readonly { id: string }[]): AssistantTurn {
  const turn = turns.find((t): t is AssistantTurn => 'role' in t && t.role === 'assistant')
  if (!turn) throw new Error('expected an assistant turn to exist')
  return turn
}

function setup() {
  const fake = fakeFetch()
  const { result } = renderHook(() => {
    const chat = useChat()
    const showMore = useShowMoreOffers(chat, { fetchImpl: fake.fetchImpl })
    return { chat, showMore }
  })
  return { result, ...fake }
}

function offersPage(startRank: number, count: number) {
  return Array.from({ length: count }, (_, i) =>
    makeRankedOffer({ rank: startRank + i, offerId: `OFF-${startRank + i}` }),
  )
}

/** Seeds a turn exactly where a real search leaves one before "show more" is ever clicked: a
 * `search-id` and a full first page of 10 (task 25's own cap). */
function seedFirstPage(result: { current: { chat: ChatController } }): string {
  let turnId = ''
  act(() => {
    turnId = result.current.chat.submit('lisbon') ?? ''
  })
  act(() => {
    result.current.chat.applyEvent(turnId, { type: 'search-id', data: { searchId: 'search-1' } })
    result.current.chat.applyEvent(turnId, { type: 'ranked-offers', data: offersPage(1, 10) })
  })
  return turnId
}

describe('useShowMoreOffers', () => {
  it('E1 — the next batch appends below the existing 10, in rank order, without disturbing them', async () => {
    const { result, push } = setup()
    const turnId = seedFirstPage(result)

    push(jsonResponse(offersPage(11, 10), 200))
    act(() => result.current.showMore.showMore(turnId))
    await flush()

    const offers = assistantTurnOf(result.current.chat.turns).stages.rankedOffers ?? []
    expect(offers).toHaveLength(20)
    expect(offers.map((o) => o.offerId)).toEqual(offersPage(1, 20).map((o) => o.offerId))
    expect(assistantTurnOf(result.current.chat.turns).stages.moreOffersStatus).toBe('idle')
  })

  it('E2 — a page shorter than requested marks the list exhausted, not an error', async () => {
    const { result, push } = setup()
    const turnId = seedFirstPage(result)

    push(jsonResponse(offersPage(11, 3), 200))
    act(() => result.current.showMore.showMore(turnId))
    await flush()

    const turn = assistantTurnOf(result.current.chat.turns)
    expect(turn.stages.rankedOffers).toHaveLength(13)
    expect(turn.stages.moreOffersStatus).toBe('exhausted')
  })

  it('E3 — an expired searchId (404) reports "expired", leaving the existing offers untouched', async () => {
    const { result, push } = setup()
    const turnId = seedFirstPage(result)

    push(new Response(null, { status: 404 }))
    act(() => result.current.showMore.showMore(turnId))
    await flush()

    const turn = assistantTurnOf(result.current.chat.turns)
    expect(turn.stages.moreOffersStatus).toBe('expired')
    expect(turn.stages.rankedOffers).toHaveLength(10)
  })

  it('E4 — two rapid clicks before the first resolves make only one request', async () => {
    const { result, push, calls } = setup()
    const turnId = seedFirstPage(result)

    push(jsonResponse(offersPage(11, 10), 200))
    act(() => {
      result.current.showMore.showMore(turnId)
      result.current.showMore.showMore(turnId)
    })
    await flush()

    expect(calls).toHaveLength(1)
    expect(assistantTurnOf(result.current.chat.turns).stages.rankedOffers).toHaveLength(20)
  })
})

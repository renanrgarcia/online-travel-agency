import { act, renderHook } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'

import { useSearchChat } from './useSearchChat'
import type { AssistantTurn } from './types'
import { fakeEventSourceFactory } from '../test/fakeEventSource'
import {
  EXPLANATION_JSON,
  PARSED_INTENT_JSON,
  RANKED_OFFERS_JSON,
  SUPPLIER_RESULT_GDS_JSON,
  SUPPLIER_RESULT_LCC_FAILED_JSON,
  SUPPLIER_RESULT_NDC_JSON,
} from '../test/fixtures'
import { LanguageProvider } from '../i18n/LanguageProvider'

/**
 * One test per eval in docs/features/02-frontend/tasks/03-the-search-turn.md, driven against a fake
 * `EventSource` — the real transport is F01's own concern, already covered there. What's new here is
 * the wiring: does a real (fake) stream actually reach the chat state, stage by stage.
 */

vi.mock('../config', () => ({ getApiBaseUrl: () => '' }))

/** One hook instance, wired to one fake EventSource factory it hasn't used yet. */
function setup() {
  const factory = fakeEventSourceFactory()
  const { result, unmount } = renderHook(
    () => useSearchChat({ createEventSource: factory.create }),
    { wrapper: LanguageProvider },
  )
  return { result, unmount, source: () => factory.instance() }
}

function assistantTurnOf(turns: readonly { id: string }[]): AssistantTurn {
  const turn = turns.find((t): t is AssistantTurn => 'role' in t && t.role === 'assistant')
  if (!turn) throw new Error('expected an assistant turn to exist')
  return turn
}

describe('useSearchChat', () => {
  it('E1 — a real stream populates all four stages in contract order and completes', () => {
    const { result, source } = setup()

    act(() => {
      result.current.submit('cheapest flight from São Paulo to Lisbon')
    })
    expect(result.current.isStreaming).toBe(true)

    act(() => {
      source().emit('parsed-intent', PARSED_INTENT_JSON)
      source().emit('supplier-result', SUPPLIER_RESULT_GDS_JSON)
      source().emit('ranked-offers', RANKED_OFFERS_JSON)
      source().emit('explanation', EXPLANATION_JSON)
    })

    const turn = assistantTurnOf(result.current.turns)
    expect(turn.stages.parsedIntent?.origin).toBe('GRU')
    expect(turn.stages.supplierResults).toHaveLength(1)
    expect(turn.stages.rankedOffers).toHaveLength(3)
    expect(turn.stages.explanation?.isClean).toBe(true)

    // The server closes the connection once it's done sending; no `done` event exists.
    act(() => source().emitTransportFailure())

    expect(assistantTurnOf(result.current.turns).status).toBe('complete')
    expect(result.current.isStreaming).toBe(false)
  })

  it('E2 — each supplier-result reaches state as its own update, not batched with the others', () => {
    const { result, source } = setup()
    act(() => result.current.submit('lisbon'))

    act(() => source().emit('supplier-result', SUPPLIER_RESULT_GDS_JSON))
    expect(assistantTurnOf(result.current.turns).stages.supplierResults).toHaveLength(1)

    act(() => source().emit('supplier-result', SUPPLIER_RESULT_NDC_JSON))
    expect(assistantTurnOf(result.current.turns).stages.supplierResults).toHaveLength(2)

    act(() => source().emit('supplier-result', SUPPLIER_RESULT_LCC_FAILED_JSON))
    const results = assistantTurnOf(result.current.turns).stages.supplierResults
    expect(results).toHaveLength(3)
    // A failed supplier is still shown, not dropped (F03's locked decision).
    expect(results[2]?.status).toBe('TimedOut')
  })

  it('E3 — the turn stays streaming, with no stage populated, until the first event arrives', () => {
    const { result } = setup()
    act(() => result.current.submit('lisbon'))

    const turn = assistantTurnOf(result.current.turns)
    expect(turn.status).toBe('streaming')
    expect(turn.stages.parsedIntent).toBeUndefined()
  })

  it('E5 — ranked offers are in state before explanation arrives, not held back for it', () => {
    const { result, source } = setup()
    act(() => result.current.submit('lisbon'))

    act(() => {
      source().emit('parsed-intent', PARSED_INTENT_JSON)
      source().emit('ranked-offers', RANKED_OFFERS_JSON)
    })

    const turn = assistantTurnOf(result.current.turns)
    expect(turn.stages.rankedOffers).toHaveLength(3)
    expect(turn.stages.explanation).toBeUndefined()
    expect(turn.status).toBe('streaming')
  })

  it('E6 — unmounting closes whatever stream is still open', () => {
    const { result, unmount, source } = setup()

    act(() => result.current.submit('lisbon'))
    expect(source().closed).toBe(false)

    unmount()

    expect(source().closed).toBe(true)
  })

  it('E6 — starting a second search closes a stream left open from a stale call', () => {
    // Guards the handler itself, not just the composer-level UI lock.
    const first = fakeEventSourceFactory()
    const second = fakeEventSourceFactory()
    let useSecond = false
    const { result } = renderHook(
      () => useSearchChat({ createEventSource: (url) => (useSecond ? second : first).create(url) }),
      { wrapper: LanguageProvider },
    )

    act(() => result.current.submit('first'))
    expect(first.instance().closed).toBe(false)

    useSecond = true
    act(() => result.current.submit('second'))

    expect(first.instance().closed).toBe(true)
  })

  it('E8 — a second search after one completes creates its own turn and leaves the first intact', () => {
    const first = fakeEventSourceFactory()
    const second = fakeEventSourceFactory()
    let useSecond = false
    const { result } = renderHook(
      () => useSearchChat({ createEventSource: (url) => (useSecond ? second : first).create(url) }),
      { wrapper: LanguageProvider },
    )

    act(() => result.current.submit('first search'))
    act(() => {
      first.instance().emit('explanation', EXPLANATION_JSON)
      first.instance().emitTransportFailure()
    })
    expect(result.current.turns).toHaveLength(2) // user + assistant

    useSecond = true
    act(() => result.current.submit('second search'))

    expect(result.current.turns).toHaveLength(4)
    const [firstAssistant] = result.current.turns.filter(
      (turn): turn is AssistantTurn => turn.role === 'assistant',
    )
    expect(firstAssistant?.status).toBe('complete')
    expect(firstAssistant?.stages.explanation?.text).toBe(
      'The best value is $590.00, taking 8h with 1 stop (non-refundable).',
    )
  })

  it('reports a connection-lost failure as a failed turn with a user-facing message', () => {
    const { result, source } = setup()
    act(() => result.current.submit('lisbon'))

    act(() => source().emitTransportFailure())

    const turn = assistantTurnOf(result.current.turns)
    expect(turn.status).toBe('failed')
    expect(turn.failure?.message).toBe('Connection lost. Try your search again.')
  })

  it('does not fail the turn over a single malformed frame — the stream is still alive', () => {
    const { result, source } = setup()
    act(() => result.current.submit('lisbon'))

    act(() => source().emit('ranked-offers', '{ not json'))

    expect(assistantTurnOf(result.current.turns).status).toBe('streaming')
  })
})

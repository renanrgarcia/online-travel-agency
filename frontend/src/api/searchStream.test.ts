import { describe, expect, it, vi } from 'vitest'

import { buildSearchStreamUrl, openSearchStream, type StreamFailure } from './searchStream'
import type { SearchStreamEvent } from './contract'
import { fakeEventSourceFactory } from '../test/fakeEventSource'
import {
  ERROR_JSON,
  EXPLANATION_JSON,
  PARSED_INTENT_ACCENTED_JSON,
  PARSED_INTENT_JSON,
  RANKED_OFFERS_JSON,
  SUPPLIER_RESULT_GDS_JSON,
  SUPPLIER_RESULT_LCC_FAILED_JSON,
  SUPPLIER_RESULT_NDC_JSON,
} from '../test/fixtures'

/** One test per eval in docs/features/02-frontend/tasks/01-scaffold-and-sse-client.md. */
function openWithFake(query = 'cheapest flight from São Paulo to Lisbon') {
  const events: SearchStreamEvent[] = []
  const failures: StreamFailure[] = []
  const onComplete = vi.fn()
  const factory = fakeEventSourceFactory()

  const handle = openSearchStream(
    query,
    {
      onEvent: (event) => events.push(event),
      onFailure: (failure) => failures.push(failure),
      onComplete,
    },
    { createEventSource: factory.create },
  )

  return { events, failures, onComplete, handle, source: factory.instance() }
}

describe('openSearchStream', () => {
  it('E1 — yields all four event types, in contract order, with payloads parsed', () => {
    const { events, source } = openWithFake()

    source.emit('parsed-intent', PARSED_INTENT_JSON)
    source.emit('supplier-result', SUPPLIER_RESULT_GDS_JSON)
    source.emit('ranked-offers', RANKED_OFFERS_JSON)
    source.emit('explanation', EXPLANATION_JSON)

    expect(events.map((event) => event.type)).toEqual([
      'parsed-intent',
      'supplier-result',
      'ranked-offers',
      'explanation',
    ])

    const [intent, supplier, offers, explanation] = events
    if (intent?.type !== 'parsed-intent') throw new Error('expected parsed-intent')
    if (supplier?.type !== 'supplier-result') throw new Error('expected supplier-result')
    if (offers?.type !== 'ranked-offers') throw new Error('expected ranked-offers')
    if (explanation?.type !== 'explanation') throw new Error('expected explanation')

    expect(intent.data).toEqual({
      origin: 'GRU',
      destination: 'LIS',
      departureDate: '2027-03-12',
      passengerCount: 2,
      language: 'en',
    })
    expect(supplier.data.supplierName).toBe('GDS')
    expect(supplier.data.status).toBe('Succeeded')
    expect(offers.data).toHaveLength(3)
    expect(offers.data[0]?.offerId).toBe('LCC-002')
    expect(offers.data[0]?.price).toBe(590)
    expect(explanation.data.isClean).toBe(true)
  })

  it('E2 — surfaces each of three supplier-result events individually', () => {
    const { events, source } = openWithFake()

    source.emit('supplier-result', SUPPLIER_RESULT_GDS_JSON)
    source.emit('supplier-result', SUPPLIER_RESULT_NDC_JSON)
    source.emit('supplier-result', SUPPLIER_RESULT_LCC_FAILED_JSON)

    const supplierEvents = events.filter((event) => event.type === 'supplier-result')
    expect(supplierEvents).toHaveLength(3)
    expect(supplierEvents.map((event) => event.data.supplierName)).toEqual(['GDS', 'NDC', 'LCC'])
    // The failing one keeps its distinct status and reason rather than collapsing into "failed".
    expect(supplierEvents[2]?.data.status).toBe('TimedOut')
    expect(supplierEvents[2]?.data.reason).toBe('exceeded 5s timeout')
  })

  it('E3 — delivers each event as it arrives rather than batching at the end', async () => {
    const { events, source } = openWithFake()
    const gap = () => new Promise((resolve) => setTimeout(resolve, 10))

    source.emit('parsed-intent', PARSED_INTENT_JSON)
    expect(events).toHaveLength(1)

    await gap()
    source.emit('supplier-result', SUPPLIER_RESULT_GDS_JSON)
    expect(events).toHaveLength(2)

    await gap()
    source.emit('ranked-offers', RANKED_OFFERS_JSON)
    expect(events).toHaveLength(3)

    await gap()
    source.emit('explanation', EXPLANATION_JSON)
    expect(events).toHaveLength(4)
  })

  it('E4 — carries an accented payload through intact', () => {
    const { events, source } = openWithFake()

    source.emit('parsed-intent', PARSED_INTENT_ACCENTED_JSON)

    const [event] = events
    if (event?.type !== 'parsed-intent') throw new Error('expected parsed-intent')
    expect(event.data.origin).toBe('São Paulo')
    expect(event.data.destination).toBe('Lisboa')
    expect(event.data.language).toBe('pt-BR')
  })

  it('E5 — reports a server error event as an event, not as a transport failure', () => {
    const { events, failures, source } = openWithFake()

    source.emit('error', ERROR_JSON)

    expect(failures).toEqual([])
    const [event] = events
    if (event?.type !== 'error') throw new Error('expected error event')
    expect(event.data.message).toBe('missing origin')
  })

  it('E5 — reports a transport failure as a failure, not as a server error event', () => {
    const { events, failures, source } = openWithFake()

    source.emitTransportFailure()

    expect(events).toEqual([])
    expect(failures).toEqual([{ kind: 'connection-lost' }])
  })

  it('E6 — closes the underlying connection when the consumer abandons the stream', () => {
    const { handle, source } = openWithFake()

    expect(source.closed).toBe(false)
    handle.close()

    expect(source.closed).toBe(true)
  })

  it('E6 — closing twice closes the underlying connection only once', () => {
    const { handle, source } = openWithFake()

    handle.close()
    handle.close()

    expect(source.closeCount).toBe(1)
  })

  it('E7 — reports a malformed payload and keeps the stream alive', () => {
    const { events, failures, source } = openWithFake()

    source.emit('parsed-intent', PARSED_INTENT_JSON)
    source.emit('ranked-offers', '{ this is not json')
    source.emit('explanation', EXPLANATION_JSON)

    expect(failures).toHaveLength(1)
    expect(failures[0]?.kind).toBe('malformed-payload')
    if (failures[0]?.kind !== 'malformed-payload') throw new Error('expected malformed-payload')
    expect(failures[0].eventType).toBe('ranked-offers')
    expect(failures[0].raw).toBe('{ this is not json')

    // The events either side of the bad frame still arrived.
    expect(events.map((event) => event.type)).toEqual(['parsed-intent', 'explanation'])
  })

  it('E8 — ignores an unknown event type without throwing', () => {
    const { events, failures, source } = openWithFake()

    expect(() => source.emit('some-future-event', '{"anything":true}')).not.toThrow()

    expect(events).toEqual([])
    expect(failures).toEqual([])
    expect(source.hasListenerFor('some-future-event')).toBe(false)
  })

  it('treats a connection close after a terminal event as normal completion, not a failure', () => {
    const { failures, onComplete, source } = openWithFake()

    source.emit('explanation', EXPLANATION_JSON)
    source.emitTransportFailure() // the server closing the stream looks like this

    expect(onComplete).toHaveBeenCalledOnce()
    expect(failures).toEqual([])
    expect(source.closed).toBe(true)
  })

  it('stops listening once closed, so a late frame is ignored', () => {
    const { events, handle, source } = openWithFake()

    handle.close()
    source.emit('parsed-intent', PARSED_INTENT_JSON)

    expect(events).toEqual([])
  })

  it('does not reconnect after a transport failure — it closes for good', () => {
    const { source } = openWithFake()

    source.emitTransportFailure()

    // EventSource has no flag to disable retry; closing is how retry is suppressed, so that an
    // abandoned search never silently re-runs the pipeline and re-spends the supplier budget.
    expect(source.closed).toBe(true)
  })
})

describe('buildSearchStreamUrl', () => {
  it('encodes the query and defaults to same-origin', () => {
    expect(buildSearchStreamUrl('São Paulo to Lisbon')).toBe(
      '/api/search/stream?q=S%C3%A3o%20Paulo%20to%20Lisbon',
    )
  })

  it('prefixes a configured base URL', () => {
    expect(buildSearchStreamUrl('lisbon', 'https://api.example.net')).toBe(
      'https://api.example.net/api/search/stream?q=lisbon',
    )
  })
})

import { SEARCH_STREAM_EVENT_TYPES, TERMINAL_EVENT_TYPES, type SearchStreamEvent } from './contract'

/**
 * The slice of `EventSource` this client uses. Narrowed to an interface so tests can drive a fake
 * (F01 evals run with no server), and so nothing here depends on the global being present.
 */
export interface EventSourceLike {
  addEventListener(type: string, listener: (event: Event) => void): void
  close(): void
}

export type EventSourceFactory = (url: string) => EventSourceLike

/**
 * Why the stream stopped or why one frame was dropped. Kept separate from the contract's own `error`
 * event: "the pipeline reported a failure" and "the connection died" are different facts and F06
 * renders them differently.
 */
export type StreamFailure =
  | { kind: 'malformed-payload'; eventType: string; raw: string; cause: unknown }
  | { kind: 'connection-lost' }

export interface SearchStreamHandlers {
  onEvent: (event: SearchStreamEvent) => void
  /** The server finished normally — a terminal event arrived and the connection then closed. */
  onComplete?: () => void
  onFailure?: (failure: StreamFailure) => void
}

export interface SearchStreamOptions {
  /** Origin of the API. Empty means same-origin. Task F03 supplies the deployed one. */
  baseUrl?: string
  createEventSource?: EventSourceFactory
}

export interface SearchStreamHandle {
  close(): void
}

export function buildSearchStreamUrl(query: string, baseUrl = ''): string {
  return `${baseUrl}/api/search/stream?q=${encodeURIComponent(query)}`
}

const defaultFactory: EventSourceFactory = (url) => new EventSource(url)

/**
 * Opens the search stream and reports each event as it arrives.
 *
 * Two behaviours here are less obvious than they look:
 *
 * **The `error` name is overloaded.** A server-sent `event: error` frame and an `EventSource`
 * transport failure both dispatch to a listener registered for `'error'`. They're told apart by
 * shape: the server's arrives as a `MessageEvent` carrying `data`, a transport failure as a bare
 * `Event`. Getting this wrong would render "we couldn't parse your query" as a network outage.
 *
 * **Retry is suppressed by closing.** `EventSource` reconnects automatically and the standard offers
 * no flag to stop it. A silent reconnect would re-run the whole pipeline — including supplier calls
 * that spend the look-to-book budget — so the first transport error closes the connection for good.
 */
export function openSearchStream(
  query: string,
  handlers: SearchStreamHandlers,
  options: SearchStreamOptions = {},
): SearchStreamHandle {
  const { baseUrl = '', createEventSource = defaultFactory } = options
  const source = createEventSource(buildSearchStreamUrl(query, baseUrl))

  let closed = false
  let sawTerminalEvent = false

  const close = () => {
    if (closed) return
    closed = true
    source.close()
  }

  for (const eventType of SEARCH_STREAM_EVENT_TYPES) {
    source.addEventListener(eventType, (event) => {
      if (closed) return

      // A transport failure also lands here when eventType is 'error'; it has no `data`.
      const raw = readData(event)
      if (raw === undefined) {
        if (eventType === 'error') handleTransportFailure()
        return
      }

      let parsed: unknown
      try {
        parsed = JSON.parse(raw)
      } catch (cause) {
        // One bad frame must not discard the events that already arrived (F01 E7).
        handlers.onFailure?.({ kind: 'malformed-payload', eventType, raw, cause })
        return
      }

      if (TERMINAL_EVENT_TYPES.includes(eventType)) sawTerminalEvent = true
      handlers.onEvent({ type: eventType, data: parsed } as SearchStreamEvent)
    })
  }

  function handleTransportFailure() {
    // The server never sends a `done` event; it just closes. So a transport error *after* a terminal
    // event is a normal end of stream, and one before it is a genuine interruption.
    const completed = sawTerminalEvent
    close()
    if (completed) handlers.onComplete?.()
    else handlers.onFailure?.({ kind: 'connection-lost' })
  }

  return { close }
}

function readData(event: Event): string | undefined {
  const data: unknown = (event as MessageEvent).data
  return typeof data === 'string' ? data : undefined
}

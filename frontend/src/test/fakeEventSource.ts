import type { EventSourceLike } from '../api/searchStream'

/**
 * A driveable stand-in for `EventSource`, so the F01 evals run with no server and no network.
 *
 * It models the one piece of `EventSource` behaviour that actually matters to this client: a
 * server-sent frame arrives as a `MessageEvent` carrying `data`, while a transport failure arrives as
 * a bare `Event` — both dispatched under the name `error` when the frame is an error frame.
 */
export class FakeEventSource implements EventSourceLike {
  readonly url: string
  closeCount = 0

  private readonly listeners = new Map<string, ((event: Event) => void)[]>()

  constructor(url: string) {
    this.url = url
  }

  get closed(): boolean {
    return this.closeCount > 0
  }

  addEventListener(type: string, listener: (event: Event) => void): void {
    const existing = this.listeners.get(type)
    if (existing) existing.push(listener)
    else this.listeners.set(type, [listener])
  }

  close(): void {
    this.closeCount += 1
  }

  /** Deliver a server-sent frame: `event: <type>` / `data: <data>`. */
  emit(type: string, data: string): void {
    this.dispatch(type, new MessageEvent(type, { data }))
  }

  /** Deliver a transport failure — a bare `Event`, no `data`, dispatched as `error`. */
  emitTransportFailure(): void {
    this.dispatch('error', new Event('error'))
  }

  /** Whether anything is listening for a given event name. */
  hasListenerFor(type: string): boolean {
    return (this.listeners.get(type)?.length ?? 0) > 0
  }

  private dispatch(type: string, event: Event): void {
    for (const listener of this.listeners.get(type) ?? []) listener(event)
  }
}

/** Captures the instance so a test can drive it after `openSearchStream` returns. */
export function fakeEventSourceFactory(): {
  create: (url: string) => FakeEventSource
  instance: () => FakeEventSource
} {
  let created: FakeEventSource | undefined
  return {
    create: (url) => {
      created = new FakeEventSource(url)
      return created
    },
    instance: () => {
      if (!created) throw new Error('No FakeEventSource has been created yet')
      return created
    },
  }
}

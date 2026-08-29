# F01 — Scaffold and typed SSE client

**Roadmap step:** 1. Foundations
**Source doc:** `docs/reference/06-api-sse-contract.md`, `docs/reference/08-package-versions.md`
**Depends on:** nothing

## Goal

Stand up the Vite project and a typed client for the SSE contract — parsing and event typing only, no
UI. The mirror of backend task 12: learn the transport in isolation, before anything real depends on it.

## Scope

- `npm create vite@latest . -- --template react-ts` in `frontend/`, plus Vitest.
- A client that opens the stream and surfaces a **discriminated union** of the four event types, so
  `switch`ing on the event type narrows the payload and an unhandled case is a compile error.
- TypeScript types matching the contract exactly: `parsed-intent`, `supplier-result`, `ranked-offers`,
  `explanation`, plus the `error` event.

## Out of scope

- Any rendering — task 02 owns the UI, task 03 owns the wiring.
- Reconnection and `Last-Event-ID` replay. The backend doesn't assign event IDs, and a resumed search
  would re-run the pipeline rather than continue it; a dropped stream is a new search, not a resume.

## Evals

| ID | Setup | Expected | Why it matters |
|---|---|---|---|
| E1 | A hand-written stream of all four event types in contract order | The client yields four typed events, in order, payloads parsed | Baseline against the documented byte format |
| E2 | Three `supplier-result` events | All three surface individually | N-of-a-kind is the one cardinality easy to collapse by accident |
| E3 | Events arriving with real gaps between them | Each surfaces as it arrives, not batched at the end | The property backend task 12 E3 proves server-side, now proven client-side. A client that buffers wastes a streaming API |
| E4 | A payload containing `São Paulo` | Arrives intact | UTF-8 through the whole chain; the target market hits this immediately |
| E5 | An `error` event | Surfaces as an error, distinguishable from a transport failure | "The query couldn't be parsed" and "the network died" need different UI |
| E6 | The consumer abandons the stream | The underlying connection closes | Backend task 13 E8 stops work on disconnect — that only helps if the client actually disconnects |
| E7 | A malformed `data:` line | Reported, and does not take down the stream | One bad frame shouldn't discard the events that already arrived |
| E8 | An unknown event type | Ignored without throwing | A server that adds a fifth event type mustn't break an older client |

### Locked decisions

- **`EventSource`, not `fetch` + a manual reader.** The endpoint is a plain GET, `EventSource` handles
  the framing, and the contract was written for it. The trade-off — no custom headers — costs nothing
  here, since there is no auth to send.
- **No client-side retry.** `EventSource` reconnects by default; that would silently re-run a whole
  search, including supplier calls that consume the look-to-book budget. Disable it and let task 06
  decide what a user sees instead.

## Done when

All eight evals pass with no server running.

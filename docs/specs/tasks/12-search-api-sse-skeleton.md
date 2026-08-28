# 12 — Search API SSE skeleton

**Roadmap step:** 6. API + SSE
**Source doc:** `docs/06-api-sse-contract.md`
**Depends on:** nothing new

## Superseded by task 13

`SkeletonEventSource.cs` and its tests were removed once task 13 wired the real pipeline into
`GET /api/search/stream` — the same route can only do one thing, so the skeleton's hard-coded events and
the real ones can't coexist. This note, and the eval table below, stay as a record of a real, deliberate
step: verifying SSE transport mechanics (headers, framing, streaming, cancellation, UTF-8) in isolation
*before* wiring anything real into it caught nothing wrong here, but the discipline is what task 13
builds on with confidence. Unlike task 08 (deleted outright, at the owner's request, once the frontend
made it redundant), this task's code is retired by replacement, not by request — worth the distinction.

## Goal

Stand up `FlightAi.Api` and a `GET /api/search/stream` endpoint that holds an SSE connection open and
emits one hard-coded event — before any pipeline logic. Isolates "learn SSE and ASP.NET Core" from
"wire the real pipeline" (task 13).

## Scope

- Minimal ASP.NET Core project under `backend/src/FlightAi.Api`.
- The route, returning `text/event-stream`, emitting one correctly framed hard-coded event.

## Out of scope (comes later)

- Real event types and pipeline logic — task 13.

## Evals

| ID | Setup | Expected | Why it matters |
|---|---|---|---|
| E1 | `curl -N` the endpoint | Response `Content-Type` is `text/event-stream` | The contract's baseline |
| E2 | Response headers | Exactly one `Content-Type` header | `docs/09-lessons-learned.md` documents a real double-header bug here — pin it before it recurs |
| E3 | Endpoint emits 3 events spaced 500ms apart | Client observes them ≈500ms apart, not all at once at the end | Genuine streaming, not a buffered response pretending. The single most common way this task silently fails |
| E4 | Event framing | Matches `docs/06-api-sse-contract.md` byte for byte (`event:`/`data:` lines, blank-line terminator) | A browser `EventSource` is unforgiving about framing |
| E5 | Client disconnects mid-stream | Server observes cancellation and stops work | Otherwise abandoned searches burn supplier budget in task 13 |
| E6 | Payload containing a UTF-8 accented string (e.g. `São Paulo`) | Arrives intact | Brazilian-market data will hit this immediately |

### Locked decisions

- Response buffering is explicitly disabled for this endpoint; E3 verifies it rather than assuming.

## Done when

All six evals pass. E3 especially — a buffered "stream" passes every other check while being useless.

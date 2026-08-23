# 12 — Search API SSE skeleton

**Roadmap step:** 6. API + SSE
**Source doc:** `docs/06-api-sse-contract.md`
**Depends on:** 08 (console demo pipeline, as the logic you're now exposing over HTTP)

## Goal

Stand up the ASP.NET Core project and a `GET /api/search/stream` endpoint that can hold open a
Server-Sent Events connection and emit *one* hard-coded event — before wiring any real pipeline logic
through it. This isolates "learning SSE and ASP.NET Core" from "wiring the real pipeline," which is task
13.

## Scope

- A minimal ASP.NET Core project (`FlightAi.Api` or similar).
- The `GET /api/search/stream` route, returning `text/event-stream`, emitting one hard-coded event in
  the correct SSE wire format (check `docs/06-api-sse-contract.md` for exact framing).
- Confirm you can hit it with `curl` and see the event, and that the connection behaves like a stream
  (not a single buffered response).

## Out of scope (comes later)

- Emitting the real four event types (`parsed-intent`, `supplier-result` × N, `ranked-offers`,
  `explanation`) — task 13.
- Any actual pipeline logic — this task is transport only.

## Done when

- `curl -N http://localhost:<port>/api/search/stream` shows the hard-coded event arrive, and the
  connection stays open in a way consistent with genuine streaming (not everything buffered and flushed
  at once — verify this, don't assume it, since ASP.NET Core response buffering is one of the easier
  things to get wrong here).

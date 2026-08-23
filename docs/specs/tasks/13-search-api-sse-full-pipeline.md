# 13 — Search API, full pipeline

**Roadmap step:** 6. API + SSE
**Source doc:** `docs/06-api-sse-contract.md`
**Depends on:** 12 (SSE skeleton), 08 (pipeline), 10–11 (agents)

## Goal

Wire the real pipeline (suppliers → ranking → intent/explanation agents) through the SSE endpoint from
task 12, emitting the four event types in true completion order as each stage of the pipeline finishes —
not buffered and dumped at the end.

## Scope

- `parsed-intent` — emitted once `IntentAgentFactory`'s agent (task 10) resolves the request.
- `supplier-result` × N — one per connector as `SupplierFanOutOrchestrator` (task 06) reports each
  connector's result, including failed/timed-out connectors per task 06's reporting.
- `ranked-offers` — emitted once `OfferScorer` (task 03) has ranked the combined results.
- `explanation` — emitted once `ExplanationAgentFactory`'s agent (task 11) has produced prose, rendered
  through task 02's renderer before it leaves the server (the client should only ever see resolved
  prices, never a token).
- Match the exact payload shapes documented in `docs/06-api-sse-contract.md`.

## Out of scope (comes later)

- The booking saga — tasks 14–16, a separate Functions app entirely.
- A real model — still `OfflineChatClient` for this task; task 17 swaps it.

## Done when

- `curl -N` against the real endpoint shows all four event types arrive in the documented order, with
  `supplier-result` events arriving as each connector finishes rather than all at once at the end (you
  should be able to see this timing with connectors that have different artificial delays, from task 05).
- A test (integration-level, not necessarily a full HTTP test) proves a resolved price never appears as
  an unresolved token in the `explanation` event payload.

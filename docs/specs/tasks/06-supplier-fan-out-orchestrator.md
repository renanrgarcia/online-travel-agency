# 06 — Supplier fan-out orchestrator

**Roadmap step:** 3. Suppliers
**Source doc:** `docs/03-suppliers-and-budget.md`
**Depends on:** 05 (mock supplier connectors)

## Goal

Build `SupplierFanOutOrchestrator`: call all registered connectors in parallel, apply a per-connector
timeout, and degrade gracefully when some connectors fail or time out rather than failing the whole
search. This is your first real async-coordination code in the system.

## Scope

- Accept a list of `ISupplierConnector` and a search request; call all of them concurrently.
- Enforce a per-connector timeout (config value, doesn't need to be elaborate).
- On partial failure (one connector times out or throws), return the offers from the connectors that
  succeeded rather than failing the entire search — this is the "partial-result degradation" behavior
  named in `docs/01-architecture-overview.md`.
- Surface *which* connectors failed/timed out somehow (a result object, not just silently dropping
  offers) — later tasks (13, the SSE pipeline) will want to report per-supplier status.

## Out of scope (comes later)

- Budget enforcement across the whole search (how many supplier calls you're allowed to make) — task 07.
- Circuit breaking a consistently-failing connector — also task 07.

## Done when

- A unit test proves that with two healthy connectors, both sets of offers come back.
- A unit test proves that with one connector configured to fail (using task 05's failure marker) and one
  healthy, the healthy connector's offers still come back and the failure is reported, not thrown as an
  unhandled exception.
- A unit test proves a connector that never completes (simulate with a long delay) is cut off at the
  configured timeout and doesn't block the other connectors' results.

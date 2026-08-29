# 05 — Mock supplier connectors

**Roadmap step:** 3. Suppliers
**Source doc:** `docs/reference/03-suppliers-and-budget.md`
**Depends on:** 04 (connector interface)

## Goal

Implement mock connectors against task 04's interface — enough variety to exercise fan-out realistically
in task 06, with no supplier credentials and fully reproducible behaviour.

## Scope

- All three connectors `docs/reference/01-architecture-overview.md` names explicitly: `MockGdsConnector`,
  `MockNdcConnector`, `MockLccConnector` — an aggregator, a full-service-direct, and a budget-carrier
  shape, returning deterministic, hand-built offers.
- A deliberate failure-injection convention, mirroring the booking saga's (`docs/reference/07-booking-saga.md`).
- Configurable artificial latency so task 06's timeout can be tested.

## Out of scope (comes later)

- Parallel invocation — task 06. Real wire formats — out of scope for the whole system, per
  `docs/reference/01-architecture-overview.md`.

## Evals

| ID | Input | Expected | Why it matters |
|---|---|---|---|
| E1 | Ordinary request, run twice | Byte-identical offer sets both runs | Reproducibility is the reason these exist |
| E2 | Two different connectors, same request | Different offer sets, no ID collisions between them | Task 06 merges these; colliding IDs would corrupt task 01's per-offer tokens |
| E3 | Request carrying `FAIL-SEARCH` | Failure result with a reason, no exception thrown | Task 04 E3, exercised for real |
| E4 | Request carrying `FAIL-SEARCH` | The *other* connector still succeeds | Failure is per-connector, never global |
| E5 | Connector configured with a 5s delay, called with a token cancelled at 100ms | Returns promptly as cancelled, not after 5s | Cancellation is honoured, not merely accepted — task 06's timeout depends on this |
| E6 | Every offer returned | Has a unique, stable ID and every field task 03's scorer reads | Feeds tasks 06–08 without surprises |
| E7 | Offer prices | Vary enough that ranking order differs by weight (as in task 03's fixtures) | Fixtures where every offer scores alike would make later ranking/integration testing prove nothing |

### Locked decisions

- **Failure markers are per-connector, not global**: a request whose `Destination` contains
  `FAIL-SEARCH-{ConnectorName}` (e.g. `FAIL-SEARCH-NDC`) fails only the connector named in the marker.
  A blanket `FAIL-SEARCH` with no connector name would fail every connector checking the same field,
  which contradicts E4 — failure has to be triggerable per-connector from a single shared
  `SearchRequest`, since both connectors see the same request. Task 16 uses plain `FAIL-ORDER` and
  `FAIL-TICKET` for the booking saga instead, because that marker lives in an offer ID already scoped
  to one connector's own offer — no equivalent ambiguity there.
- Offer IDs are prefixed per connector (e.g. `NDC-`, `LCC-`) to guarantee E2.
- Latency is injected via `Task.Delay` honouring the `CancellationToken`, caught and translated to
  `SupplierSearchResult.Cancelled()` — never left to propagate as an exception, consistent with task
  04's "failures are returned, not thrown."

## Done when

All seven evals pass.

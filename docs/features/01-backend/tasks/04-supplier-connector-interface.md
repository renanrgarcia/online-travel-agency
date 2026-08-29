# 04 — Supplier connector interface

**Roadmap step:** 3. Suppliers
**Source doc:** `docs/reference/03-suppliers-and-budget.md`
**Depends on:** nothing new

## Goal

Define the seam every supplier integration talks through: `ISupplierConnector` and the canonical
`Offer`. Getting this contract right is what lets a real supplier be added later without touching
anything above it.

## Scope

- The canonical `Offer` — the real one, replacing task 03's stub. Scope to what
  `docs/reference/03-suppliers-and-budget.md` and `docs/reference/04-ranking.md` actually consume.
- The canonical `SearchRequest` — origin, destination, date, passenger count, and `Language` (task 10's
  intent agent produces this shape; this task defines it, since it's also every connector's search
  input). Both live in `Models/` alongside every other data type — see
  `docs/features/01-backend/tasks/README.md`'s note on layer-vs-domain folders.
- `ISupplierConnector` with an async search method returning a result type that can express **partial
  failure** — a connector that returned some offers and then failed is a real case.
- Contract only. No implementations beyond a test double.

## Out of scope (comes later)

- Real connectors — task 05. Fan-out — task 06.

## Evals

Since this task is mostly a contract, the evals are about the contract's *expressiveness*: write a
throwaway test double and prove each state below is representable without exceptions or nulls.

| ID | Scenario the result type must express | Why it matters |
|---|---|---|
| E1 | Full success with N offers | The baseline |
| E2 | Success with **zero** offers (supplier had nothing, but answered fine) | Must be distinguishable from failure — "no flights" is a valid answer, not an error |
| E3 | Outright failure with a reason, zero offers | Task 06 needs the reason to report per-supplier status |
| E4 | Partial success — some offers **and** a failure reason | The case most easily designed out by accident; a connector that pages results can fail midway |
| E5 | A cancelled call (caller's `CancellationToken` fired) distinguishable from a supplier failure | Task 06 enforces timeouts via cancellation; conflating the two would misattribute the fault to the supplier |
| E6 | Every state above is representable **without throwing** | Exceptions as control flow would make task 06's partial-degradation logic unreadable |
| E7 | `Offer` carries every field `OfferScorer` (task 03) reads, and every field `PriceReferenceStore` (task 01) registers | Prevents discovering a missing field three tasks later |
| E8 | `Offer` also carries `ExpiresAt` (`DateTimeOffset`) | The point past which a quoted price can no longer be trusted to still be bookable — added after the fact once it was noticed missing; neither `OfferScorer` nor `PriceReferenceStore` consume it yet, so it's not covered by E7's "every field consumed" check, but a real offer without an expiry is an omission, not a minimal design |

### Locked decisions

- **Failures are returned, not thrown.** A supplier failing is an expected outcome in this system, not
  an exceptional one. Reserve exceptions for programmer error.
- `ISupplierConnector` exposes a stable `Name` used as the key in task 06's per-supplier reporting and
  task 07's circuit-breaker state.
- Every search method takes a `CancellationToken`.

## Done when

All eight evals are demonstrated against a test double, and the interface is referenced nowhere else
yet.

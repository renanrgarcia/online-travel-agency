# 06 — Supplier fan-out orchestrator

**Roadmap step:** 3. Suppliers
**Source doc:** `docs/03-suppliers-and-budget.md`
**Depends on:** 05 (mock connectors)

## Goal

Build `SupplierFanOutOrchestrator`: call every connector in parallel, enforce a per-connector timeout,
and degrade to partial results rather than failing the whole search. Your first real async-coordination
code here.

## Scope

- Concurrent invocation of all registered connectors.
- A per-connector timeout, enforced via cancellation.
- Partial-result degradation, with per-supplier status surfaced (task 13's `supplier-result` events
  need it).

## Out of scope (comes later)

- Budget and circuit breaking — task 07.

## Evals

| ID | Setup | Expected | Why it matters |
|---|---|---|---|
| E1 | Two healthy connectors | All offers from both, both reported succeeded | Baseline |
| E2 | One healthy, one failing (`FAIL-SEARCH`) | Healthy connector's offers returned; failing one reported failed with its reason; **no exception escapes** | The degradation guarantee |
| E3 | One healthy (fast), one hanging past the timeout | Returns at ≈ the timeout, not the hang duration; hung one reported timed-out | The timeout actually bounds latency |
| E4 | Two connectors, each delayed 300ms, timeout 1s | Total elapsed ≈ 300ms, not ≈ 600ms | Proves genuine parallelism rather than sequential awaits — easy to get wrong and invisible without timing |
| E5 | Timed-out connector's report | Distinguishable from a supplier-reported failure | Task 04 E5 carried through; misattributing a timeout as supplier fault would poison task 07's breaker |
| E6 | All connectors fail | Empty offer set returned successfully, all reported failed — not an exception | "Everything failed" is still a valid answer the API must be able to stream |
| E7 | Zero connectors registered | Empty result, no exception | Degenerate case |
| E8 | Any run | Every registered connector appears exactly once in the status report | Task 13 emits one `supplier-result` per connector; a missing or duplicated entry is a client-visible bug |
| E9 | Offers from multiple connectors | Merged with no ID collisions, order deterministic | Task 03's ranking must receive a stable input |

### Locked decisions

- **Timeout is per connector, not for the whole fan-out.** One slow supplier must not consume the
  budget of the others.
- A timeout is reported as its own status, distinct from failure (E5).
- Merge order is by connector registration order, then by offer ID — deterministic, so task 08's output
  is reproducible.

## Done when

All nine evals pass, E4 especially — if it fails, the code is sequential and the whole task's premise is
unmet.

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
| E2 | One healthy, one failing (`FAIL-SEARCH-{Name}`, per task 05) | Healthy connector's offers returned; failing one reported failed with its reason; **no exception escapes** | The degradation guarantee |
| E3 | One healthy (fast), one hanging past the timeout | Returns at ≈ the timeout, not the hang duration; hung one reported timed-out | The timeout actually bounds latency |
| E4 | Two connectors, each delayed 300ms, timeout 1s | Total elapsed ≈ 300ms, not ≈ 600ms | Proves genuine parallelism rather than sequential awaits — easy to get wrong and invisible without timing |
| E5 | Timed-out connector's report | Distinguishable from a supplier-reported failure | Task 04 E5 carried through; misattributing a timeout as supplier fault would poison task 07's breaker |
| E6 | All connectors fail | Empty offer set returned successfully, all reported failed — not an exception | "Everything failed" is still a valid answer the API must be able to stream |
| E7 | Zero connectors registered | Empty result, no exception | Degenerate case |
| E8 | Any run | Every registered connector appears exactly once in the status report | Task 13 emits one `supplier-result` per connector; a missing or duplicated entry is a client-visible bug |
| E9 | Offers from multiple connectors | Merged with no ID collisions, order deterministic | Task 03's ranking must receive a stable input |

### Locked decisions

- **Timeout is per connector, not for the whole fan-out** — and, since task 07's correction, genuinely
  per-connector in *duration* too, not just independently triggered. Each connector's timeout comes
  from its own `SupplierPolicy` (task 07), not one shared value. One slow supplier must not consume the
  budget of the others, and a real supplier's contracted latency has no reason to match another's.
- A timeout is reported as its own status, distinct from failure (E5).
- **A connector reporting `Cancelled` (task 04) is a timeout only when the caller's own token has not
  fired.** Both arrive at the orchestrator as the same `SupplierOutcome.Cancelled`, since the connector
  sees only one linked token and can't tell which side cancelled it. The orchestrator disambiguates by
  checking the caller's token, and reports caller-initiated cancellation as its own status — blaming a
  supplier for a client that hung up would poison task 07's breaker exactly the way E5 warns about.
- **A connector that throws despite task 04's "failures are returned, not thrown" is caught and
  reported as failed.** The contract says connectors shouldn't throw; the orchestrator does not get to
  assume every future connector honours it, and E2's "no exception escapes" is unconditional.
- Merge order is by connector registration order, then by offer ID — deterministic, so ranking (task 03)
  and everything downstream of it receives a reproducible input.

## Done when

All nine evals pass, E4 especially — if it fails, the code is sequential and the whole task's premise is
unmet.

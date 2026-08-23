# 07 — Look-to-book budget and circuit breaker

**Roadmap step:** 3. Suppliers
**Source doc:** `docs/03-suppliers-and-budget.md`
**Depends on:** 06 (fan-out orchestrator)

## Goal

Build `LookToBookBudget` and `SupplierCircuitBreaker` — the guardrails that stop supplier calls running
unchecked — and wire both into the orchestrator.

## Scope

- `LookToBookBudget`: tracks search calls against a configured ceiling and refuses further calls past it.
- `SupplierCircuitBreaker`: after N consecutive failures from one connector, stop calling it for a
  cooldown rather than burning the timeout budget every search.
- Both wired into task 06's orchestrator.

## Out of scope

- Persistence across restarts. In-memory is correct for this system; note it as a known limitation.

## Evals

| ID | Setup | Expected | Why it matters |
|---|---|---|---|
| E1 | Budget ceiling 3, make 3 calls | All 3 permitted | Baseline |
| E2 | Same, make a 4th | Refused, and the refusal is reported (not thrown, not silent) | The ceiling binds and is observable |
| E3 | After reset window elapses | Calls permitted again | The ceiling is a rate, not a permanent kill |
| E4 | Breaker threshold 2; one connector fails twice consecutively | Breaker opens; connector not invoked on the 3rd search | The breaker stops wasting time on a dead supplier |
| E5 | Same run as E4 | The *other* connector is still invoked normally | Breaker state is per connector, never global |
| E6 | After cooldown elapses | Failing connector is invoked again | The breaker recovers rather than permanently disabling a supplier |
| E7 | Connector fails once, then succeeds, then fails once (threshold 2) | Breaker stays closed | Threshold counts *consecutive* failures; a success resets the count |
| E8 | Breaker open for a connector | Its status is reported as skipped/circuit-open, distinct from failed and from timed-out | Task 13 streams this; "not called" is different information from "called and failed" |
| E9 | Timeouts from task 06 | Count toward the breaker's failure tally | A supplier that always times out is as dead as one that errors |
| E10 | Budget exhausted mid-fan-out | Already-started calls complete; no new ones start | Partial results still beat none |
| E11 | Full slice: one healthy connector, one flapping, tight budget | Search still returns usable offers | Integration check across tasks 04–07 |

### Locked decisions

- **Breaker counts consecutive failures; any success resets to zero** (E7).
- **Timeouts count as failures for the breaker** (E9), even though task 06 reports them distinctly to
  the client. Different audiences, different granularity.
- Budget refusal and breaker-open are **reported statuses**, not exceptions — consistent with task 04's
  locked decision.

## Done when

All eleven evals pass, including E11 end to end.

# 07 — Look-to-book budget and circuit breaker

**Roadmap step:** 3. Suppliers
**Source doc:** `docs/03-suppliers-and-budget.md`
**Depends on:** 06 (supplier fan-out orchestrator)

## Goal

Build `LookToBookBudget` and `SupplierCircuitBreaker` — the two guardrails that keep supplier calls from
running unchecked. This closes out the suppliers step of the roadmap.

## Scope

- `LookToBookBudget`: tracks how many "look" (search) calls have been made against a supplier relative
  to actual bookings, and can refuse further calls once a configured ratio/limit is exceeded. This
  models a real constraint — suppliers meter and rate-limit search volume relative to conversion.
- `SupplierCircuitBreaker`: given a connector that's failing repeatedly (use task 05's failure marker to
  simulate this in tests), stop calling it for a cooldown period instead of retrying every search and
  wasting the timeout budget from task 06.
- Wire both into the orchestrator from task 06 so a search respects budget and breaker state.

## Out of scope (comes later)

- Persisting budget/breaker state across process restarts — an in-memory implementation is enough for
  this system; note it as a known limitation rather than building durability you don't need yet.

## Done when

- A unit test proves the budget refuses a call once its configured limit is hit, and allows calls again
  once the limit resets (however you define reset — document the policy you chose).
- A unit test proves the circuit breaker opens (stops calling) after a configured number of consecutive
  failures from one connector, and that the *other* connector is unaffected.
- A unit test proves the breaker closes again (resumes calling) after its cooldown period elapses.
- Run the full suppliers slice (tasks 04–07) together against a scenario with one healthy connector, one
  intermittently failing connector, and a tight budget — confirm the search still returns usable results.

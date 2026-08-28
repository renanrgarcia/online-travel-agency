# 07 — Look-to-book budget and circuit breaker

**Roadmap step:** 3. Suppliers
**Source doc:** `docs/reference/03-suppliers-and-budget.md`
**Depends on:** 06 (fan-out orchestrator)

## Goal

Build `LookToBookBudget` and `SupplierCircuitBreaker` — the guardrails that stop supplier calls running
unchecked — and wire both into the orchestrator, **per connector**, via `SupplierPolicy`.

## Revision note

The first pass at this task shared one `LookToBookBudget` and one `SupplierCircuitBreaker` across every
connector the orchestrator held. The breaker was already internally safe (a `Dictionary` keyed by
connector name kept each connector's failure count isolated), but the budget was a single counter with
no per-connector tracking at all — meaning every connector drew from the same pool. That contradicted
`docs/reference/03-suppliers-and-budget.md`'s own description of the budget as **"a per-session, per-supplier
shopping-call budget"**, and it also meant the timeout was one shared duration rather than something a
real, differently-contracted supplier could be given its own value for.

The fix: `SupplierPolicy` (in `Models/Suppliers/`) bundles one connector's `Timeout`, and *optionally*
its own `BudgetCeiling`/`BudgetWindow` and `BreakerFailureThreshold`/`BreakerCooldown`. The orchestrator
takes `IReadOnlyDictionary<string, SupplierPolicy>` keyed by connector name instead of one shared
`budget`/`breaker`, and builds one `LookToBookBudget` and one `SupplierCircuitBreaker` **per connector**
internally. `SupplierCircuitBreaker` itself got simpler as a result — one instance now means one
connector's state, so the `Dictionary<string, BreakerState>` and every `supplierName` parameter were
removed. A connector with no matching entry in `policies` fails fast at construction, rather than
discovering a missing configuration mid-search.

## Scope

- `LookToBookBudget`: tracks search calls against a configured ceiling and refuses further calls past it.
  Unchanged in shape from the first pass — it was always single-connector-shaped; what changed is that
  the orchestrator now creates one per connector instead of one shared instance.
- `SupplierCircuitBreaker`: after N consecutive failures, stop calling it for a cooldown rather than
  burning its timeout every search. Simplified per the revision note above.
- `SupplierPolicy`: per-connector timeout, plus optional budget and breaker configuration.
- All three wired into task 06's orchestrator, keyed by connector name.

## Out of scope

- Persistence across restarts, and sharing state across multiple instances of the host process. Both
  remain in-memory and per-process; note as a known limitation, not something this task solves.
- Loading `SupplierPolicy` values from external configuration (appsettings, a feature-flag service) —
  real supplier contracts would decide these numbers, and they'd need to change without a redeploy.
  Worth doing before this meters anything real; out of scope for a reference implementation.

## Evals

| ID | Setup | Expected | Why it matters |
|---|---|---|---|
| E1 | Budget ceiling 3, make 3 calls | All 3 permitted | Baseline |
| E2 | Same, make a 4th | Refused, and the refusal is reported (not thrown, not silent) | The ceiling binds and is observable |
| E3 | After reset window elapses | Calls permitted again | The ceiling is a rate, not a permanent kill |
| E4 | Two connectors, only one configured with a breaker (threshold 2); that one fails twice consecutively | Its circuit opens; connector not invoked on the 3rd search | Proves policies are genuinely per-connector — one connector can have a breaker at all while another has none |
| E5 | Same run as E4 | The connector with **no** breaker configured is invoked normally throughout | Breaker state is per connector, never global — demonstrated here by *absence* of a breaker, not just isolated state within a shared one |
| E6 | After cooldown elapses | Failing connector is invoked again | The breaker recovers rather than permanently disabling a supplier |
| E7 | Connector fails once, then succeeds, then fails once (threshold 2), tested directly against `SupplierCircuitBreaker` with no orchestrator involved | Breaker stays closed | Threshold counts *consecutive* failures; a success resets the count |
| E8 | Breaker open for one connector (threshold 1); the other connector genuinely fails via its own marker | Open connector's status is `SkippedCircuitOpen`; the other's is `Failed` — distinct from each other and from `TimedOut` | Task 13 streams this; "not called" is different information from "called and failed" |
| E9 | One connector configured with a strict timeout + a breaker; the other with a generous timeout and no breaker; the strict one hangs past its timeout twice | Its circuit opens from timeouts alone | A supplier that always times out is as dead as one that errors — and this only makes sense once timeout is genuinely per-connector |
| E10 | One connector's budget ceiling is 1; search it twice | The second search reports that connector `SkippedBudgetExhausted`; the other connector (no budget configured) still succeeds | Confirms exhaustion is scoped to the connector whose budget ran out, not a shared pool — the shared-pool version of this scenario no longer exists by construction |
| E11 | Full slice: one connector with budget+breaker configured and flapping, one healthy connector with only a budget configured | Search still returns the healthy connector's usable offers | Integration check across tasks 04–07, now exercising genuinely heterogeneous per-connector configuration |
| E12 | A connector registered with the orchestrator but absent from `policies` | Constructing the orchestrator throws `ArgumentException` immediately | A missing policy should fail loudly at startup, not surface as a confusing runtime behavior mid-search |

### Locked decisions

- **Configuration is per connector, via `SupplierPolicy`, never shared.** This is the corrected design;
  see the revision note above for why the earlier shared version was wrong, not just simpler.
- **Budget and breaker are each optional per connector** (both fields of a pair, or neither) — a
  connector can run with no budget, no breaker, or both, independently of what any other connector has.
  This is what lets task 06's own evals construct an orchestrator with no guardrails at all.
- **Breaker counts consecutive failures; any success resets to zero** (E7).
- **Timeouts count as failures for the breaker** (E9), even though task 06 reports them distinctly to
  the client. Different audiences, different granularity.
- Budget refusal and breaker-open are **reported statuses**, not exceptions — consistent with task 04's
  locked decision. A *missing policy* is the one exception to that: it's a configuration error, not a
  runtime outcome a client should ever see, so it throws at construction (E12).

## Done when

All twelve evals pass, including E11 end to end.

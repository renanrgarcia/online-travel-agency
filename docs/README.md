# docs

The standalone spec for a .NET 10 + React reference implementation of an AI-assisted flight search and
booking system. This folder is self-contained — it doesn't reference or depend on any external
document. It exists so the design decisions behind the implementation survive independently of the
code itself.

## Reading order, if rebuilding from scratch

1. **`01-architecture-overview.md`** — the projects, how they relate, the three ways to run the system.
2. **`02-price-integrity.md`** — the single most important design decision: how the system guarantees a
   language model can never author a number a user sees.
3. **`03-suppliers-and-budget.md`** — the supplier adapter interface, parallel fan-out, the look-to-book
   budget, and the circuit breaker.
4. **`04-ranking.md`** — why ranking is a deterministic scoring function, not a model call.
5. **`05-agents-and-intent.md`** — the two AI touchpoints, and how to swap the offline mock for a real
   model.
6. **`06-api-sse-contract.md`** — the search API's Server-Sent Events contract.
7. **`07-booking-saga.md`** — the Durable Functions booking saga: steps, compensation, idempotency.
8. **`08-package-versions.md`** — verified package versions and API surfaces, so you don't have to
   rediscover them by trial and error.
9. **`09-lessons-learned.md`** — four real bugs found while building this, and the general pattern
   behind them.

Each doc names the specific source files it describes, so once you're rebuilding, you can go
file-by-file against a doc rather than trying to hold the whole system in your head at once.

## Rebuilding step by step

**`specs/`** reorders this same material into build order instead of reading order:
[`specs/macro-scenario.md`](specs/macro-scenario.md) lays out an 8-step implementation roadmap, and
[`specs/tasks/`](specs/tasks/README.md) breaks it into 17 individually scoped, testable tasks — start at
`specs/tasks/01`.

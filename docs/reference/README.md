# reference — the system as designed

How the system works and why it's shaped this way. These documents describe the system as built, not
the order to build it in — that's [`../features/`](../features/README.md).

Each doc names the specific source files it describes, so once you're rebuilding, you can go
file-by-file against a doc rather than trying to hold the whole system in your head at once.

## Reading order

1. **[`01-architecture-overview.md`](01-architecture-overview.md)** — the projects, how they relate,
   the ways to run the system.
2. **[`02-price-integrity.md`](02-price-integrity.md)** — the single most important design decision:
   how the system guarantees a language model can never author a number a user sees.
3. **[`03-suppliers-and-budget.md`](03-suppliers-and-budget.md)** — the supplier adapter interface,
   parallel fan-out, the look-to-book budget, and the circuit breaker.
4. **[`04-ranking.md`](04-ranking.md)** — why ranking is a deterministic scoring function, not a model
   call.
5. **[`05-agents-and-intent.md`](05-agents-and-intent.md)** — the two AI touchpoints, and how to swap
   the offline mock for a real model.
6. **[`06-api-sse-contract.md`](06-api-sse-contract.md)** — the search API's Server-Sent Events
   contract.
7. **[`07-booking-saga.md`](07-booking-saga.md)** — the Durable Functions booking saga: steps,
   compensation, idempotency.
8. **[`08-package-versions.md`](08-package-versions.md)** — verified package versions and API surfaces,
   so you don't have to rediscover them by trial and error.
9. **[`09-lessons-learned.md`](09-lessons-learned.md)** — real bugs found while building this, and the
   general pattern behind them.
10. **[`10-frontend-architecture.md`](10-frontend-architecture.md)** — the chat UI: the turn state
    model, the two transports (SSE for search, polling for booking), and the degraded-state rendering
    policy.
11. **[`11-bilingual-ui.md`](11-bilingual-ui.md)** — how the UI's own chrome follows the query's
    detected language without an i18n library, and without ever retranslating a completed turn.
12. **[`12-supplier-api-options.md`](12-supplier-api-options.md)** — current mock-provider behavior,
    free-versus-paid flight API options, and the recommended path to a real Duffel adapter.

## A note on drift

These describe the system as it was designed. Where the implementation has since diverged
deliberately, the task card that caused the divergence records it — the task cards are the newer
source of truth when the two disagree, and the reference doc should be corrected rather than the code
bent to match it.

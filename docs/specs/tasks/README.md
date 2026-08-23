# Tasks

Seventeen tasks, numbered in the order to implement them — the same order as
[`../macro-scenario.md`](../macro-scenario.md). Each task is scoped to be buildable and testable on its
own before moving to the next; each names the `docs/0N-*.md` file that is its source of truth, what's
explicitly out of scope (because a later task owns it), and how to know you're done.

## The eval discipline

Every task carries an **Evals** table: numbered acceptance criteria, each a fixed input and an expected
observable output, written *before* the implementation exists. Tests assert exactly these, and test
names carry the eval ID (`E1_...`, `E2_...`) so a failure points at the criterion it violates.

This ordering is the whole point. A test written after the code tends to assert whatever the code
already does — it passes by construction and proves nothing. An eval written first is an external
target the implementation has to meet, so when the two disagree, **the implementation is wrong, not
the eval.**

Each task also has a **Locked decisions** section recording choices the source docs left open (display
formats, tie-breaks, sign conventions). These exist so the evals have something concrete to assert
against instead of deferring to whatever the code produces. If you disagree with a locked decision,
change it in the task note first, then let the failing test drive the code change.

| # | Task | Roadmap step |
|---|---|---|
| [01](01-price-reference-tokens.md) | Price reference tokens | 1. Price integrity core |
| [02](02-explanation-placeholder-renderer.md) | Explanation placeholder renderer | 1. Price integrity core |
| [03](03-offer-scoring.md) | Offer scoring | 2. Ranking |
| [04](04-supplier-connector-interface.md) | Supplier connector interface | 3. Suppliers |
| [05](05-mock-supplier-connectors.md) | Mock supplier connectors | 3. Suppliers |
| [06](06-supplier-fan-out-orchestrator.md) | Supplier fan-out orchestrator | 3. Suppliers |
| [07](07-look-to-book-budget-and-circuit-breaker.md) | Look-to-book budget and circuit breaker | 3. Suppliers |
| [08](08-console-demo-pipeline.md) | Console demo pipeline | 4. Console demo |
| [09](09-offline-chat-client.md) | Offline chat client | 5. AI layer, offline |
| [10](10-intent-agent.md) | Intent agent | 5. AI layer, offline |
| [11](11-explanation-agent.md) | Explanation agent | 5. AI layer, offline |
| [12](12-search-api-sse-skeleton.md) | Search API SSE skeleton | 6. API + SSE |
| [13](13-search-api-sse-full-pipeline.md) | Search API, full pipeline | 6. API + SSE |
| [14](14-booking-functions-project-setup.md) | Booking Functions project setup | 7. Booking saga |
| [15](15-booking-saga-orchestrator.md) | Booking saga orchestrator | 7. Booking saga |
| [16](16-booking-saga-compensation-and-idempotency.md) | Compensation and idempotency | 7. Booking saga |
| [17](17-swap-in-real-model.md) | Swap in a real model | 8. Real model |

Work through them in order — each one assumes the tasks before it are done. If a task feels too big to
finish in one sitting, that's a signal to stop and split it yourself rather than push through; these
tasks are scoped for learning, not for velocity.

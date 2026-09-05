# Backend tasks

Numbered in the order to implement them — the same order as [`../README.md`](../README.md), this
feature's roadmap. Each task is scoped to be buildable and testable on its own before moving to the
next; each names the `docs/reference/0N-*.md` file that is its source of truth, what's explicitly out of
scope (because a later task owns it), and how to know you're done.

The eval discipline these cards follow is described once, in
[`../../README.md`](../../README.md) — read it before writing a test against any of them.

| # | Task | Roadmap step |
|---|---|---|
| [01](01-price-reference-tokens.md) | Price reference tokens | 1. Price integrity core |
| [02](02-explanation-placeholder-renderer.md) | Explanation placeholder renderer | 1. Price integrity core |
| [03](03-offer-scoring.md) | Offer scoring | 2. Ranking |
| [04](04-supplier-connector-interface.md) | Supplier connector interface | 3. Suppliers |
| [05](05-mock-supplier-connectors.md) | Mock supplier connectors | 3. Suppliers |
| [06](06-supplier-fan-out-orchestrator.md) | Supplier fan-out orchestrator | 3. Suppliers |
| [07](07-look-to-book-budget-and-circuit-breaker.md) | Look-to-book budget and circuit breaker | 3. Suppliers |
| [09](09-offline-chat-client.md) | Offline chat client | 4. AI layer, offline |
| [10](10-intent-agent.md) | Intent agent | 4. AI layer, offline |
| [11](11-explanation-agent.md) | Explanation agent | 4. AI layer, offline |
| [12](12-search-api-sse-skeleton.md) | Search API SSE skeleton | 5. API + SSE |
| [13](13-search-api-sse-full-pipeline.md) | Search API, full pipeline | 5. API + SSE |
| [18](18-comparative-decision-support.md) | Comparative decision support | 6. Decision support |
| [14](14-booking-functions-project-setup.md) | Booking Functions project setup | 7. Booking saga |
| [15](15-booking-saga-orchestrator.md) | Booking saga orchestrator | 7. Booking saga |
| [16](16-booking-saga-compensation-and-idempotency.md) | Compensation and idempotency | 7. Booking saga |
| [24](24-switch-booking-saga-to-durable-task-scheduler.md) | Switch the booking saga to the Durable Task Scheduler | 7. Booking saga |
| [19](19-cors-for-the-browser-client.md) | CORS for the browser client | 8. Safe to expose |
| [20](20-rate-limiting-and-quota-protection.md) | Rate limiting and quota protection | 8. Safe to expose |
| [21](21-server-authoritative-offer-prices.md) | Server-authoritative offer prices | 8. Safe to expose |
| [23](23-error-handling-and-diagnostics.md) | Error handling and diagnostics | 8. Safe to expose |
| [17](17-swap-in-real-model.md) | Swap in a real model | 9. Real model |
| [25](25-duffel-supplier-connector.md) | Duffel supplier connector | 10. Real supplier integration |

Functions infrastructure (Bicep + CI/CD) was originally task 22 here; it's now
[`../../03-infra/tasks/01-functions-infrastructure-and-cicd.md`](../../03-infra/tasks/01-functions-infrastructure-and-cicd.md)
— moved once it became clear provisioning Azure resources is a different kind of work from application
code, not specific to this feature. Depends on task 16 above.

## Numbering vs. build order

The table is in **build order**; the numbers are in the order the tasks were *written*. Tasks 18–21 were
specified after 01–17 already existed (task 22 also was, before it moved to `03-infra`), 23 later still,
and 24 later again — a real cost finding after the fact, not a build-order gap — and renumbering to close
the gaps would have invalidated every task reference already embedded in code comments and test names,
the same reasoning that left the 08 gap alone.

Two orderings matter and aren't implied by the numbers:

- **18 before 17.** Task 17's twenty-run stress test is the real proof that the price-integrity boundary
  survives a real model. Comparison facts should exist by then so that test covers them too.
- **19 before feature 02's task 03.** The frontend cannot reach the API cross-origin without CORS.

Task `08` (console demo pipeline) was built, used for exploration, and then deliberately removed once
its job was done — verifying the pipeline by hand moves to task 13's real API from here on. See
`git log --follow -- docs/specs/tasks/08-console-demo-pipeline.md` for its history (that was its path
before the docs were reorganised into `reference/` and `features/`).

## A note on `FlightAi.Core`'s folder structure

Tasks 01–07 were originally implemented with folders by *domain concept* — `Offers/`, `Pricing/`,
`Ranking/`, `Suppliers/` — each holding everything related to one idea. It then moved to folders by
*technical layer* — `Models/`, `Interfaces/`, `Services/` — at the owner's preference, with every file
moving without any behavior change.

If you're rebuilding from scratch rather than following this repo's history, you can pick either
convention — both are legitimate, and the trade-off is real: domain folders keep one concept's pieces
together at the cost of scattering technical roles; layer folders keep one technical role together at
the cost of scattering one concept across three folders. Neither is "more correct."

In practice this project settled on **both, nested**: layer folders at the top (`Models/`,
`Interfaces/`, `Services/`), each containing domain subfolders (`Offers/`, `Pricing/`, `Ranking/`,
`Suppliers/`) — e.g. `Services/Suppliers/SupplierFanOutOrchestrator.cs`. A flat layer folder scales
badly once it holds a dozen unrelated files; nesting domain folders inside each layer keeps the layer
boundary while restoring the "find everything about X in one place" property domain folders gave up.

Namespaces match the full folder path (`FlightAi.Core.Services.Suppliers`), changed from an earlier
flat-at-the-layer-level convention once the nesting settled — so a file's namespace and its location on
disk always agree.

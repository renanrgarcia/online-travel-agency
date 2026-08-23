# Implementation roadmap — build order for learning

`docs/` (the parent folder) documents the system in *reading* order: architecture first, then the
invariant that constrains everything else, then outward to suppliers, ranking, AI, the API, and the
booking saga. That's the right order to understand the system. It is not the right order to *build* it.

This roadmap reorders the same material into *build* order: the pieces with zero external dependencies
first, so each step can be verified with `dotnet test` alone before the next step adds a new kind of
complexity (async fan-out, then AI, then a web server, then Azure Functions). Each step below points at
the `docs/0N-*.md` file that is its source of truth, and is broken into individual tasks in
[`tasks/`](tasks/README.md), numbered `01`–`17` in the same order as this roadmap — start at
`tasks/01` and work forward.

## 1. Price integrity core

**Source:** `docs/02-price-integrity.md`

`PriceReferenceStore` + `ExplanationPlaceholderRenderer`. Pure C#, no I/O. This is the one invariant
everything else depends on — a language model must never author a digit the traveller sees — so getting
it right first means every later piece can lean on it instead of re-deriving trust boundaries.

## 2. Ranking

**Source:** `docs/04-ranking.md`

`OfferScorer` + `ScoringWeights`. Also pure and deterministic, trivially testable. A good second step
because it's conceptually simple but teaches the "why isn't this just a model call" reasoning that
recurs throughout the system.

## 3. Suppliers

**Source:** `docs/03-suppliers-and-budget.md`

`ISupplierConnector`, mock connectors, `SupplierFanOutOrchestrator`, `LookToBookBudget`,
`SupplierCircuitBreaker`. First taste of async/parallel fan-out and failure handling, still with no real
external dependency since the connectors are mocked.

## 4. Console demo — first vertical slice

Wire steps 1–3 together in a console app. This is the first end-to-end run of the pipeline, and it
surfaces integration mistakes before web or AI complexity sits on top of them.

## 5. AI layer, offline first

**Source:** `docs/05-agents-and-intent.md`

`IntentAgentFactory`, `ExplanationAgentFactory`, backed by `OfflineChatClient`. Still zero API keys;
validates the agent plumbing against the price-integrity boundary from step 1 before any real model is
involved.

## 6. API + SSE

**Source:** `docs/06-api-sse-contract.md`

First real ASP.NET Core surface, streaming the pipeline's stages to a client via Server-Sent Events.

## 7. Booking saga

**Source:** `docs/07-booking-saga.md`

The most infrastructure-heavy step — Azurite, Durable Functions, Azure Functions Core Tools — so it's
worth saving for when the rest of the system is stable and you're not debugging two unfamiliar things at
the same time.

## 8. Swap in a real model

**Source:** `docs/05-agents-and-intent.md`, `docs/08-package-versions.md`

Gemini's free tier or the Microsoft Foundry pattern, once the offline path works end to end.

---

Not a numbered step, but worth doing alongside the roadmap: the frontend (React) can slot in any time
after step 6, in parallel, if you want a visual target sooner than the roadmap otherwise gives you.
`docs/09-lessons-learned.md` is worth (re-)reading right before step 6 and step 7 specifically — three of
the four documented bugs live there, and they're cheaper to avoid than to rediscover.

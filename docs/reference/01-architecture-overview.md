# Architecture overview

A .NET 10 + React reference implementation of an AI-assisted flight search and booking system. The
central design bet: keep search, ranking, pricing, and ticketing fully deterministic, and use AI only
at the two edges — turning a natural-language query into a structured search, and turning a ranked,
already-priced result set into readable prose. Nothing in between calls a model.

## The projects

```
src/
  FlightAi.Core/                Domain + deterministic logic. No AI dependency at all.
    Models/                     Data types: Offer, SearchRequest, ScoringWeights, SupplierSearchResult, ...
    Interfaces/                 ISupplierConnector.
    Services/                   OfferScorer, PriceReferenceStore, ExplanationPlaceholderRenderer,
                                 SupplierFanOutOrchestrator, LookToBookBudget, SupplierCircuitBreaker,
                                 and the mock connectors — everything with behavior, grouped by
                                 technical role (layer) rather than by domain concept. See
                                 docs/features/01-backend/tasks/README.md's note on this choice.
  FlightAi.Agents/              The AI layer. Depends on Microsoft.Agents.AI + Microsoft.Extensions.AI.Abstractions.
    Models/                     IntentResult — the AI layer's own supporting types.
    Services/                   IntentAgentFactory (NL -> typed SearchRequest via RunAsync<T>),
                                 ExplanationAgentFactory (ranked offers -> prose, opaque tokens only),
                                 OfflineChatClient (offline IChatClient stand-in — swap for a real
                                 model, see 05-agents-and-intent.md). Same Models/Services convention
                                 as FlightAi.Core.
  FlightAi.Api/                 Minimal API. GET /api/search/stream — Server-Sent Events, one per pipeline stage.
  FlightAi.Booking.Functions/   Azure Durable Functions saga for the booking flow.
tests/
  FlightAi.Tests/                xUnit. Determinism, price-integrity, budget, circuit-breaker, timeout.
frontend/                       React + TypeScript + Vite chat UI. See 10-frontend-architecture.md.
  src/
    api/                         Typed clients for both backends: the search SSE stream and the
                                   booking HTTP contract, plus the shared payload types.
    chat/                        The turn model and every component that renders a turn -- search,
                                   booking, offer cards -- plus the two network-wiring hooks
                                   (useSearchChat, useBookingFlow) that drive it.
    i18n/                        Strings for both languages and the language-context machinery. See
                                   11-bilingual-ui.md.
```

`FlightAi.Core` has zero AI dependency by design — it's the part that has to be boring, testable, and
explainable. `FlightAi.Agents` is the only project that touches a language model, and it only ever
produces two things: a typed object (intent parsing) or prose built from opaque tokens (explanation).
Neither agent ever sees a raw price, and neither agent's output can reach a user without passing back
through deterministic code first.

## The two backends, one frontend

1. **API + React frontend** (`FlightAi.Api` + `frontend/`) — the whole search pipeline exposed as a
   live HTTP endpoint that streams results as Server-Sent Events, consumed by a real browser UI. See
   `06-api-sse-contract.md` for the wire contract and `10-frontend-architecture.md` for how the UI
   consumes it. A console demo project existed briefly during development to verify the deterministic
   core end to end by hand before the AI layer and the API existed; it was removed once the API took
   over that role — see `docs/features/01-backend/tasks/README.md`.
2. **Booking saga** (`FlightAi.Booking.Functions`) — a separate Azure Durable Functions app handling
   the booking flow (payment, order, ticket, confirmation) as a checkpointed, compensable state
   machine. See `07-booking-saga.md`. `frontend/` talks to this directly too, cross-origin, rather than
   proxying it through the API — a genuinely separate Azure resource, not a second hosting model for
   the same thing.

Both backends share `FlightAi.Core`. Only the API touches `FlightAi.Agents`. `frontend/` is the one
piece of the system that talks to both.

## What's deliberately not here

- **Redis-backed offer/semantic caching.** The two-cache design (a short-TTL offer cache, a semantic
  cache for LLM outputs) is real and worth building, but a Redis dependency doesn't belong in a
  `dotnet run`-and-go reference implementation. `LookToBookBudget` and the circuit breaker are the
  pieces that demonstrate the same discipline without needing external infrastructure.
- **Real supplier wire formats.** The mock connectors (`MockGdsConnector`, `MockNdcConnector`,
  `MockLccConnector`) return the canonical `Offer` shape directly — they exist to prove the fan-out
  orchestrator's contract (timeout, budget, partial results), not to parse actual Amadeus/Sabre/Duffel
  JSON.
- **A resilience library.** `SupplierCircuitBreaker` is hand-rolled on purpose, so its behavior is
  readable in one small file. Reach for Polly in a real service instead of reimplementing this.

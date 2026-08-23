# Architecture overview

A .NET 10 + React reference implementation of an AI-assisted flight search and booking system. The
central design bet: keep search, ranking, pricing, and ticketing fully deterministic, and use AI only
at the two edges — turning a natural-language query into a structured search, and turning a ranked,
already-priced result set into readable prose. Nothing in between calls a model.

## The projects

```
src/
  FlightAi.Core/                Domain + deterministic logic. No AI dependency at all.
    Offers/                     The canonical offer model and the typed SearchRequest.
    Suppliers/                  ISupplierConnector, fan-out orchestrator, look-to-book budget, circuit breaker.
    Ranking/                    OfferScorer — the weighted scoring function.
    Pricing/                    PriceReferenceStore + ExplanationPlaceholderRenderer — the price-integrity boundary.
  FlightAi.Agents/              The AI layer. Depends on Microsoft.Agents.AI + Microsoft.Extensions.AI.Abstractions.
    IntentAgentFactory.cs       NL -> typed SearchRequest via RunAsync<T>.
    ExplanationAgentFactory.cs  Ranked offers -> prose, using opaque tokens instead of numbers.
    OfflineChatClient.cs        Offline IChatClient stand-in — swap out for a real model, see 05-agents-and-intent.md.
  FlightAi.Demo/                Console app wiring all four pieces into one run.
  FlightAi.Api/                 Minimal API. GET /api/search/stream — Server-Sent Events, one per pipeline stage.
  FlightAi.Booking.Functions/   Azure Durable Functions saga for the booking flow.
tests/
  FlightAi.Tests/                xUnit. Determinism, price-integrity, budget, circuit-breaker, timeout.
web/                            React + TypeScript + Vite front end for FlightAi.Api and the booking saga.
```

`FlightAi.Core` has zero AI dependency by design — it's the part that has to be boring, testable, and
explainable. `FlightAi.Agents` is the only project that touches a language model, and it only ever
produces two things: a typed object (intent parsing) or prose built from opaque tokens (explanation).
Neither agent ever sees a raw price, and neither agent's output can reach a user without passing back
through deterministic code first.

## The three ways to run it

1. **Console demo** (`FlightAi.Demo`) — the whole pipeline in one process: intent parsing, supplier
   fan-out with simulated failures, ranking, explanation. `dotnet run --project src/FlightAi.Demo`.
2. **API + React frontend** (`FlightAi.Api` + `web/`) — the same pipeline exposed as a live HTTP
   endpoint that streams results as Server-Sent Events, consumed by a real browser UI. See
   `06-api-sse-contract.md`.
3. **Booking saga** (`FlightAi.Booking.Functions`) — a separate Azure Durable Functions app handling
   the booking flow (payment, order, ticket, confirmation) as a checkpointed, compensable state
   machine. See `07-booking-saga.md`.

All three share `FlightAi.Core`. Only the first two touch `FlightAi.Agents`.

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

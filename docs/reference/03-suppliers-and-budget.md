# Suppliers, fan-out, and the look-to-book budget

## The canonical offer model

`FlightAi.Core/Models/Offers/Offer.cs` defines one shape every supplier adapter maps onto, regardless of
whether the source is a GDS booking record or an NDC offer. This is the single highest-leverage type in
the whole system — the scorer, the price-integrity boundary, and the explanation agent all depend on
suppliers never leaking their own schema past the adapter layer. Version this type carefully in any
real system; everything downstream trusts its shape.

Offers expire (`ExpiresAt`) — this reflects the real "offer-and-order" model airlines use, not a
published fare with an indefinite shelf life. A cached offer past its expiry must be re-priced before
it reaches checkout.

## `ISupplierConnector`

One interface every supplier sits behind (`FlightAi.Core/Interfaces/Suppliers/ISupplierConnector.cs`). A GDS, an
NDC gateway, and an aggregator all implement it the same way — their wire formats never escape the
adapter. This is what makes the fan-out orchestrator supplier-agnostic.

## `SupplierFanOutOrchestrator`

Every connector is called in parallel, under its own timeout, counted against the look-to-book budget
before the call is even made. A slow or failing supplier degrades the overall result to partial data
rather than failing the whole search — one bad supplier should never stall or break an entire search.

## `LookToBookBudget`

A per-session, per-supplier shopping-call budget. Suppliers meter search requests against booking
volume — an agent that "tries a few extra date variations" without a hard cap here is a contractual and
financial incident, not a UX nicety. This counter belongs on the same dashboard as p95 latency in any
real deployment; if nobody owns the number, it grows without anyone noticing until a supplier's account
manager calls.

## `SupplierCircuitBreaker`

Hand-rolled on purpose, so its behavior is readable in one small file rather than buried in a general
resilience library's configuration surface (`FlightAi.Core/Services/Suppliers/SupplierCircuitBreaker.cs`). Reach
for Polly in a real service — this exists here to be a legible teaching example, not a production
recommendation.

## The mock connectors' failure-injection convention

All three mock connectors (`MockGdsConnector`, `MockNdcConnector`, `MockLccConnector`) are reliable by
default — none of them fails or stalls automatically. Failure is triggered explicitly, per connector, by
a request whose `Destination` contains `FAIL-SEARCH-{ConnectorName}` (e.g. `FAIL-SEARCH-NDC`), and
latency is injected via an optional constructor delay. This is a deliberate correction from an earlier
draft of this system, which had one connector auto-fail on its first N calls: that made behavior depend
on how many times a connector instance had already been called, which quietly broke the "same request
twice, byte-identical output" reproducibility this whole mock layer exists to guarantee. Explicit,
request-driven failure keeps every scenario — one supplier down, a timeout, a circuit-breaker trip —
reproducible on demand instead of timing-dependent.

The same deterministic-failure convention shows up again in the booking saga's mock activities
(`FAIL-ORDER` / `FAIL-TICKET` markers in an offer ID — see `07-booking-saga.md`), so that failure paths
are reproducible on demand in a demo instead of left to chance.

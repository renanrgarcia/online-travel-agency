# Suppliers, fan-out, and the look-to-book budget

## The canonical offer model

`FlightAi.Core/Models/Offer.cs` defines one shape every supplier adapter maps onto, regardless of
whether the source is a GDS booking record or an NDC offer. This is the single highest-leverage type in
the whole system — the scorer, the price-integrity boundary, and the explanation agent all depend on
suppliers never leaking their own schema past the adapter layer. Version this type carefully in any
real system; everything downstream trusts its shape.

Offers expire (`ExpiresAt`) — this reflects the real "offer-and-order" model airlines use, not a
published fare with an indefinite shelf life. A cached offer past its expiry must be re-priced before
it reaches checkout.

## `ISupplierConnector`

One interface every supplier sits behind (`FlightAi.Core/Interfaces/ISupplierConnector.cs`). A GDS, an
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
resilience library's configuration surface (`FlightAi.Core/Services/SupplierCircuitBreaker.cs`). Reach
for Polly in a real service — this exists here to be a legible teaching example, not a production
recommendation.

## The mock connectors' failure-injection convention

`MockGdsConnector` and `MockNdcConnector` behave reliably. `MockLccConnector` is deliberately
unreliable — its first two calls stall past any reasonable timeout on purpose, so it exists specifically
to drive the orchestrator's timeout and circuit-breaker paths in a demo, exercising both the
"one supplier stalls the whole search" case and a look-to-book budget breach in a single connector.

The same deterministic-failure convention shows up again in the booking saga's mock activities
(`FAIL-ORDER` / `FAIL-TICKET` markers in an offer ID — see `07-booking-saga.md`), so that failure paths
are reproducible on demand in a demo instead of left to chance.

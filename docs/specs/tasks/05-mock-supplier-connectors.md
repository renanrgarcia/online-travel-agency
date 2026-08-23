# 05 — Mock supplier connectors

**Roadmap step:** 3. Suppliers
**Source doc:** `docs/03-suppliers-and-budget.md`
**Depends on:** 04 (supplier connector interface)

## Goal

Implement two mock connectors against the interface from task 04 — enough variety (e.g. an NDC-style and
an LCC-style connector) to exercise fan-out logic in task 06 realistically, without needing any real
supplier credentials.

## Scope

- Two implementations of `ISupplierConnector`, each returning a small set of deterministic, hand-built
  `Offer`s so tests are reproducible.
- A deliberate failure-injection convention: a request containing a specific marker (e.g. an offer ID
  substring) should deterministically fail. This mirrors the same convention used later in the booking
  saga (`FAIL-ORDER` / `FAIL-TICKET`, see `docs/07-booking-saga.md`) — pick your own marker strings now,
  consistent between the two connectors.
- Realistic-enough latency variance if you want to exercise timeout behavior in task 06 (e.g. an
  optional artificial delay), but don't over-engineer this — a `Task.Delay` is enough.

## Out of scope (comes later)

- Calling both connectors in parallel — task 06.
- Any real supplier's actual wire format — deliberately out of scope for the whole system per
  `docs/01-architecture-overview.md`'s "what's deliberately not here."

## Done when

- A unit test proves each connector returns its expected offers for a normal request.
- A unit test proves each connector deterministically fails for a request carrying your failure marker,
  and succeeds otherwise — reproducible failure on demand is the entire point of this convention.

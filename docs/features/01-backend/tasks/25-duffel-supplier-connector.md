# 25 — Duffel supplier connector

**Roadmap step:** 10. Real supplier integration
**Source doc:** `docs/reference/12-supplier-api-options.md`, `docs/reference/03-suppliers-and-budget.md`
**Depends on:** 04 (connector interface), 07 (budget/circuit breaker), 17 (the config-driven "real
service, mocks stay forever" pattern this repeats for a second dependency)

## Goal

Add a real supplier alongside the existing mocks, without touching anything downstream of
`ISupplierConnector` — the exact boundary task 04 built for this. Duffel's test mode is a genuinely
free, permanent sandbox (not a trial), so this is buildable and demoable at zero cost — the same
free-tier ethos task 17 already established for the model layer, now applied to a second real
dependency.

## Scope

- `DuffelConnector : ISupplierConnector` in `FlightAi.Core`, calling Duffel's offer-request endpoint
  (`POST /air/offer_requests`) with a **test-mode** access token only.
- Maps Duffel's offer/slice/segment response shape into the existing `Offer` record — a real adapter
  problem, not a reformat: a Duffel offer carries one or more slices, each with one or more segments
  (individual flights). For the one-way search this project supports, `Offer.Stops` is
  `segments.Count - 1` on that single slice.
- Wired in by configuration (`Duffel:ApiKey`) in `Program.cs`, alongside the existing mocks — never
  replacing them. No key configured means `DuffelConnector` isn't registered at all; the three mocks
  alone still work exactly as they do today.
- A `SupplierPolicy` entry for `"Duffel"` in `Program.cs`'s orchestrator wiring (task 07) — a real
  external dependency needs its own timeout/budget/circuit-breaker tuning, looser than the mocks'
  effectively-instant responses.

## Out of scope (comes later, or not planned at all)

- **Round-trip or multi-slice search.** `SearchRequest` has one `DepartureDate` and no return date —
  Duffel supports multi-slice itineraries, this project's domain model doesn't yet. One-way only,
  matching what `SearchRequest` can already express.
- **Booking or ticketing against Duffel** (creating even a simulated Duffel order). This task is search
  only — the booking saga keeps its own existing simulated activities. Wiring the saga to real (test-mode)
  Duffel orders would be a separate, later task if ever pursued.
- **Going live (a production Duffel token), ever.** See Locked decisions.

## Evals

| ID | Setup | Expected | Why it matters |
|---|---|---|---|
| E1 | A real search against Duffel's test mode, one-way, one adult | At least one real (test-mode) offer maps cleanly into `Offer`, with no null/default field where Duffel actually returned a value | Proves the adapter against real response shapes, not just that it compiles against a hand-typed fixture |
| E2 | A search with an origin/destination Duffel can't resolve to an IATA code | A failed `SupplierSearchResult` with a reason, same shape as task 04 E3 — never an unhandled exception | The "failures are returned, not thrown" convention has to hold against a real provider's real error shape, not only the mocks' synthetic one |
| E3 | Duffel's test API is slow (a short configured timeout forces this) | `SupplierFanOutOrchestrator`'s existing per-connector timeout (tasks 06–07) catches it exactly like a slow mock | The orchestrator's timeout/budget/circuit-breaker machinery was built generically — this is the first real proof it actually is |
| E4 | `Duffel:ApiKey` not configured | `DuffelConnector` is not registered; a search still returns the three mocks' offers exactly as before | The locked "mocks are the permanent, always-available fallback" decision, made testable |
| E5 | Booking against a Duffel test-mode offer near its own staleness window | Duffel's own API rejects it; the saga surfaces this as a failure, never a silent retry or a silent book-at-wrong-price | Real fare quotes expire. Task 21's price-assertion window assumed mock offers effectively never do — this is the first supplier where that assumption is false |
| E6 | The repository, grepped | No Duffel token — test or otherwise — committed anywhere | The rule task 17 established for the model API key, applied to a second real credential |

### Locked decisions

- **Test mode only, forever, for this project.** Not a stepping-stone to a live token — per
  `docs/reference/12-supplier-api-options.md`, Duffel's test mode carries no expiry or quota, so there's
  no cost pressure that would ever force a move to production.
- **Additive, not a replacement.** `DuffelConnector` sits alongside the three mocks, never instead of
  them — the test suite keeps running against the deterministic mocks regardless of whether a Duffel key
  is configured, mirroring task 17's "the offline client stays in the codebase permanently."

## Done when

E1–E6 pass, and a real search against Duffel's test mode shows up visibly mixed in with the three mocks'
offers in one `ranked-offers` event.

## Deployment gate

See [`../../../deployment.md`](../../../deployment.md).

| ID | Requirement |
|---|---|
| D1 | The Duffel token lives in App Service Configuration (or Key Vault) — never in source control, never shipped to the browser |
| D2 | The deployed API returns a mixed mock+Duffel result set for a real search, confirmed end to end against the deployed environment, not only locally |
| D3 | The configured token is confirmed to be Duffel's **test**-mode type, not live, via Duffel's own dashboard/token metadata — checked directly, not assumed from which token happened to be pasted in |

# Supplier API options and the path beyond mocks

## Current implementation

Today, the system only searches mocked offers:

- `Program.cs` registers `MockGdsConnector`, `MockNdcConnector`, and `MockLccConnector`.
- They generate deterministic offers locally.
- The architecture already supports real providers through `ISupplierConnector`; the provider response is mapped into the internal `Offer` model.

## Are all flight APIs paid?

No, but "free" usually means a limited data API, a sandbox, or a free quota. It does not generally mean unlimited live fare search and booking. Re-validated live (not just re-read) on 2026-09-04:

- **Aviationstack** has a free plan with 100 requests per month and real-time aviation data. Confirmed it does not do fare search at all, at any tier — flight status, schedules, routes, and airport data only. Not a candidate for this project.
- **OpenSky** provides free aircraft-tracking data, but not fares or booking. Not a candidate.
- **Amadeus Self-Service is confirmed gone, not just "tutorials are outdated."** `developers.amadeus.com` itself now shows: "Amadeus for Developers self-service portal has been decommissioned on July 17th, this website is for Amadeus Enterprise API Portal only." What remains requires an enterprise sales conversation — not a free, self-serve signup. Not a candidate.
- **Kiwi.com's Tequila API** — not in the original version of this doc, worth ruling out explicitly: as of this check, Tequila no longer takes self-serve signups. It's invite-only partner access now, the same shape Google Flights and Skyscanner already have below. Not a candidate.
- **Google Flights and Skyscanner** are partner programs, not open public APIs. Access requires approval or commercial partnership. Not a candidate.
- **Duffel** is the one candidate left standing, and it's better than "not permanently free" made it sound. Duffel has two distinct modes on the same API:
  - **Test mode** — a separate test access token, simulated bookings, fake prices ("the prices you'll see are not real, live prices" — Duffel's own help centre). **Confirmed genuinely free: no charges of any kind in test mode.** This is not a limited trial — there's no quota or expiry mentioned anywhere in their docs; it's the permanent, intended way to build and test an integration before ever going live.
  - **Live mode** — real bookings, real charges ($3.00/confirmed order, 1% of order value for Managed Content, $2.00/paid ancillary, $0.005/search once search-to-book ratio exceeds 1,500:1). This is the mode the original version of this doc was describing as "not permanently free" — correct, but only half the picture.

Sources:

- [Aviationstack pricing](https://www.aviationstack.com/pricing)
- [Duffel pricing](https://duffel.com/pricing)
- [Duffel test mode](https://duffel.com/docs/api/overview/test-mode)
- [Duffel test mode prices are not real (help centre)](https://help.duffel.com/hc/en-gb/articles/4410085835282-Are-the-flight-prices-in-test-mode-sandbox-real)
- [OpenSky API](https://opensky-network.org/data/api)
- [Amadeus for Developers — decommission banner, read directly off the live site](https://developers.amadeus.com/)
- [Google Flights partners](https://developers.google.com/travel/flights)
- [Skyscanner partners](https://www.partners.skyscanner.net/)

## Recommended path

For a genuinely free demo, keep the current mock suppliers as the offline/local-dev fallback (mirroring the same pattern already locked in for the model layer, task 09/17: real integration behind configuration, mocks always available, never removed).

**Add `DuffelConnector : ISupplierConnector` against Duffel's test mode, using a test access token.** This is not a stopgap "free tier" that will eventually run out or start charging — test mode is a permanent, first-class part of Duffel's own product, so there is no future migration forced by cost. It also genuinely raises the bar over the existing mocks: real IATA-code validation, real multi-slice/multi-passenger itinerary shapes, a real auth flow, and real API failure modes (timeouts, rate limits, stale offers) that a hand-rolled mock doesn't reproduce — directly answering "how do we make this more real" without spending anything.

1. Create a Duffel account and generate a **test** access token from the dashboard (Developers → Access tokens) — never a live token for this project.
2. Add a `DuffelConnector : ISupplierConnector` in `FlightAi.Core`.
3. Call Duffel's offer-request endpoint (`POST /air/offer_requests`), then read back its `offers` array.
4. Map Duffel's offer/slice/segment shape into the internal `Offer` model (a real adapter problem: Duffel's slices/segments are richer than this project's flat model, see task card below).
5. Register it in `Program.cs`, behind configuration (`Duffel:ApiKey` or similar), so mocks remain the default with no key configured — the exact pattern already used for the Gemini key (task 17).
6. Keep the token only in environment variables or Azure App Service configuration/Key Vault — never committed, never shipped to the browser (the same rule task 17 already established for a different key).

The adapter should handle provider-specific differences: empty results, stale/expired offers (Duffel offers go stale and must be re-fetched before booking), currencies, stops, durations, passenger counts, timeouts, and partial failures. The rest of the system can remain unchanged because it already consumes the normalized `Offer` type — this is the same seam `ISupplierConnector` was built for from task 04 onward.

Task cards for implementing this — backend, frontend, and infra — live under each feature's `tasks/` folder; see each feature's roadmap for where they sit in build order.

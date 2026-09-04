# Supplier API options and the path beyond mocks

## Current implementation

Today, the system only searches mocked offers:

- `Program.cs` registers `MockGdsConnector`, `MockNdcConnector`, and `MockLccConnector`.
- They generate deterministic offers locally.
- The architecture already supports real providers through `ISupplierConnector`; the provider response is mapped into the internal `Offer` model.

## Are all flight APIs paid?

No, but "free" usually means a limited data API, a sandbox, or a free quota. It does not generally mean unlimited live fare search and booking.

- **Aviationstack** has a free plan with 100 requests per month and real-time aviation data. It is useful for flight status, schedules, routes, and airport data, but it is not a flight-fare search or booking API.
- **OpenSky** provides free aircraft-tracking data, but not fares or booking.
- **Duffel** is the most practical option for real offers. It has no upfront fee, but charges per confirmed order and for excessive search volume. It is not permanently free.
- **Amadeus tutorials are outdated**: Amadeus announced that its Self-Service developer portal was decommissioned on July 17. Access is now through its enterprise portal.
- **Google Flights and Skyscanner** are partner programs, not open public APIs. Access requires approval or commercial partnership.

Sources:

- [Aviationstack pricing](https://www.aviationstack.com/pricing)
- [Duffel pricing](https://duffel.com/pricing)
- [OpenSky API](https://opensky-network.org/data/api)
- [Amadeus announcement](https://developers.amadeus.com/pricing)
- [Google Flights partners](https://developers.google.com/travel/flights)
- [Skyscanner partners](https://www.partners.skyscanner.net/)

## Recommended path

For a genuinely free demo, keep the current mock suppliers and make them generate varied results from arbitrary searches. This proves the full pipeline without depending on commercial inventory.

For real fare searches, use Duffel:

1. Create a Duffel account.
2. Obtain an API token.
3. Add a `DuffelConnector : ISupplierConnector`.
4. Call Duffel's offer-search endpoint.
5. Map Duffel offers into the internal `Offer` model.
6. Register it in `Program.cs`, ideally behind configuration so mocks remain available locally.
7. Keep the API key only in environment variables or Azure configuration.

The adapter should handle provider-specific differences: empty results, expired offers, currencies, stops, durations, passenger counts, timeouts, and partial failures. The rest of the system can remain unchanged because it already consumes the normalized `Offer` type.

For this project, implement a `DuffelConnector` first and preserve the mocks as the offline fallback.

namespace FlightAi.Api;

/// <summary>
/// One offer as the <c>ranked-offers</c> SSE event actually serializes it. Deliberately narrower than
/// docs/06-api-sse-contract.md's original example (no <c>carrier</c>, <c>cabin</c>): those fields don't
/// exist anywhere in this rebuild's <c>Offer</c> or <c>SearchRequest</c> models, and adding them here
/// speculatively, with nothing upstream to populate them from, would be exactly the kind of
/// unrequested field this project avoids. <c>OfferId</c>'s connector prefix (<c>LCC-002</c>) already
/// communicates which supplier it came from.
/// </summary>
public sealed record RankedOfferView(
    int Rank,
    string OfferId,
    decimal Price,
    string Currency,
    int DurationMinutes,
    int Stops,
    bool Refundable,
    decimal Score);

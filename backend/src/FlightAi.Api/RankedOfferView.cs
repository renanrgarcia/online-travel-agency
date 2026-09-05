using FlightAi.Core.Services.Pricing;

namespace FlightAi.Api;

/// <summary>
/// One offer as the <c>ranked-offers</c> SSE event actually serializes it. Deliberately narrower than
/// docs/reference/06-api-sse-contract.md's original example (no <c>carrier</c>, <c>cabin</c>): those fields don't
/// exist anywhere in this rebuild's <c>Offer</c> or <c>SearchRequest</c> models, and adding them here
/// speculatively, with nothing upstream to populate them from, would be exactly the kind of
/// unrequested field this project avoids. <c>OfferId</c>'s connector prefix (<c>LCC-002</c>) already
/// communicates which supplier it came from.
/// <para>
/// <see cref="PriceAssertion"/> is attached to every offer, not just the explained top few (task 21) --
/// a traveller can book any ranked offer, and the booking saga only trusts a price it can verify.
/// </para>
/// <para>
/// <see cref="OriginAirport"/>/<see cref="DestinationAirport"/> (task 25 follow-up) are the specific
/// airport this offer actually uses, distinct from the searched <c>SearchRequest.Origin</c>/
/// <c>Destination</c>, which may be a metro/city code covering several -- so the client can disambiguate
/// when a real supplier's offers span different physical airports within the same search.
/// </para>
/// </summary>
public sealed record RankedOfferView(
    int Rank,
    string OfferId,
    decimal Price,
    string Currency,
    int DurationMinutes,
    int Stops,
    bool Refundable,
    decimal Score,
    PriceAssertion PriceAssertion,
    string? OriginAirport,
    string? DestinationAirport);

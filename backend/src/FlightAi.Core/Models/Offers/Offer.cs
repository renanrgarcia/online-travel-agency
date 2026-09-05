namespace FlightAi.Core.Models.Offers;

/// <summary>
/// The canonical offer model. Carries exactly what <c>OfferScorer</c> (task 03) reads and
/// <c>PriceReferenceStore</c> (task 01) registers, plus <see cref="ExpiresAt"/> — the point past which
/// this quoted price can no longer be trusted to still be bookable. See
/// docs/features/01-backend/tasks/04-supplier-connector-interface.md.
/// <para>
/// <see cref="OriginAirport"/>/<see cref="DestinationAirport"/> (task 25 follow-up) are the specific
/// airport this offer actually departs/arrives at — distinct from the traveller's searched
/// <c>SearchRequest.Origin</c>/<c>Destination</c>, which may be a metro/city code covering several
/// airports (e.g. "SAO" for São Paulo covers GRU, CGH, and VCP). A real supplier can legitimately
/// return offers from different physical airports within the same metro search; the mocks populate
/// these with the searched code itself since they have no real per-offer airport data. Optional with a
/// null default so every existing call site (every mock connector, every test fixture) keeps compiling
/// unchanged.
/// </para>
/// </summary>
public sealed record Offer(
    string OfferId,
    decimal Price,
    string Currency,
    TimeSpan Duration,
    int Stops,
    bool Refundable,
    decimal Margin,
    DateTimeOffset ExpiresAt,
    string? OriginAirport = null,
    string? DestinationAirport = null);

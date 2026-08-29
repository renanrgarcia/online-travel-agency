namespace FlightAi.Core.Models.Offers;

/// <summary>
/// The canonical offer model. Carries exactly what <c>OfferScorer</c> (task 03) reads and
/// <c>PriceReferenceStore</c> (task 01) registers, plus <see cref="ExpiresAt"/> — the point past which
/// this quoted price can no longer be trusted to still be bookable. See
/// docs/features/01-backend/tasks/04-supplier-connector-interface.md.
/// </summary>
public sealed record Offer(
    string OfferId,
    decimal Price,
    string Currency,
    TimeSpan Duration,
    int Stops,
    bool Refundable,
    decimal Margin,
    DateTimeOffset ExpiresAt);

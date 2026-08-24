namespace FlightAi.Core.Offers;

/// <summary>
/// The canonical offer model. Carries exactly what <c>OfferScorer</c> (task 03) reads and
/// <c>PriceReferenceStore</c> (task 01) registers — nothing else, deliberately. See
/// docs/specs/tasks/04-supplier-connector-interface.md.
/// </summary>
public sealed record Offer(
    string OfferId,
    decimal Price,
    string Currency,
    TimeSpan Duration,
    int Stops,
    bool Refundable,
    decimal Margin);

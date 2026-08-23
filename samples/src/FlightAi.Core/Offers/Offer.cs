namespace FlightAi.Core.Offers;

/// <summary>
/// The canonical offer model: one shape every supplier adapter maps onto,
/// regardless of whether it came from a GDS booking record or an NDC offer. This is the single
/// highest-leverage type in the whole system — the scorer, the price-integrity boundary and the
/// explanation agent all depend on suppliers never leaking their own schema past the adapter layer.
/// </summary>
public sealed record Offer
{
    public required string OfferId { get; init; }
    public required string SupplierId { get; init; }
    public required IReadOnlyList<FlightSegment> Segments { get; init; }
    public required IReadOnlyList<Layover> Layovers { get; init; }
    public required decimal TotalPrice { get; init; }
    public required string Currency { get; init; }

    /// <summary>Your margin on this offer (the scorer's w₈ term). Never shown to the traveller as-is.</summary>
    public required decimal MarginAmount { get; init; }

    public required FareRules FareRules { get; init; }

    /// <summary>
    /// Offers expire — this is the "offer-and-order" model, not a published fare.
    /// A cached offer past this instant must be re-priced before it reaches checkout.
    /// </summary>
    public required DateTimeOffset ExpiresAt { get; init; }

    /// <summary>0..1. Stand-in for a real on-time-performance / cancellation-history feed.</summary>
    public required double CarrierOnTimePerformance { get; init; }

    public int StopCount => Math.Max(0, Segments.Count - 1);

    public TimeSpan TotalDuration =>
        Segments.Count == 0 ? TimeSpan.Zero : Segments[^1].Arrival - Segments[0].Departure;

    public bool IsExpired(DateTimeOffset now) => now >= ExpiresAt;
}

public sealed record FlightSegment(
    string Carrier,
    string FlightNumber,
    string Origin,
    string Destination,
    DateTimeOffset Departure,
    DateTimeOffset Arrival);

public sealed record Layover(TimeSpan Duration, bool TerminalChange);

public sealed record FareRules(bool Refundable, bool ChangeableWithFee, decimal ChangeFee);

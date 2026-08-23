namespace FlightAi.Core.Ranking;

/// <summary>
/// The weighted-scoring ledger. Defaults anchor price at ~40%, matching the publicly
/// circulated read on Google Flights' "Best" tab (price ~40% / duration ~30% / stops ~20% / layover
/// ~10%) as a sanity check — not gospel. Ship with hand-tuned weights like these, then learn them from
/// booking outcomes.
///
/// <see cref="Margin"/> defaults to zero on purpose: it is a commercial lever,
/// and turning it on should be a deliberate, visible, versioned decision — never an accident of a
/// default value nobody looked at.
/// </summary>
public sealed record ScoringWeights
{
    public double Price { get; init; } = 0.40;
    public double Duration { get; init; } = 0.22;
    public double Stops { get; init; } = 0.14;
    public double LayoverQuality { get; init; } = 0.08;
    public double DepartureDesirability { get; init; } = 0.06;
    public double FareFlexibility { get; init; } = 0.05;
    public double CarrierReliability { get; init; } = 0.05;
    public double Margin { get; init; } = 0.0;

    public static ScoringWeights Default { get; } = new();
}

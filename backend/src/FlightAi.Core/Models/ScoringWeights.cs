namespace FlightAi.Core.Models;

/// <summary>
/// Weights for <c>OfferScorer</c>. <see cref="MarginWeight"/> defaults to zero: margin is a
/// commercial lever, turned on deliberately by whoever configures weights, never silently included.
/// See docs/04-ranking.md and docs/specs/tasks/03-offer-scoring.md.
/// </summary>
public sealed record ScoringWeights(
    decimal PriceWeight = 1m,
    decimal DurationWeight = 1m,
    decimal StopsWeight = 1m,
    decimal MarginWeight = 0m)
{
    public static readonly ScoringWeights Default = new();
}

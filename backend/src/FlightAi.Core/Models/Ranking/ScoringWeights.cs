namespace FlightAi.Core.Models.Ranking;

/// <summary>
/// Weights for <c>OfferScorer</c>. <see cref="MarginWeight"/> defaults to zero: margin is a
/// commercial lever, turned on deliberately by whoever configures weights, never silently included.
/// See docs/reference/04-ranking.md and docs/features/01-backend/tasks/03-offer-scoring.md.
/// </summary>
public sealed record ScoringWeights(
    decimal PriceWeight = 1m,
    decimal DurationWeight = 1m,
    decimal StopsWeight = 1m,
    decimal MarginWeight = 0m)
{
    public static readonly ScoringWeights Default = new();
}

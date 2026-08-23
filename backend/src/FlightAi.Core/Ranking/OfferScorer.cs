namespace FlightAi.Core.Ranking;

/// <summary>
/// Minimal offer shape sufficient to score. Replaced by the canonical Offer in task 04 — see
/// docs/specs/tasks/04-supplier-connector-interface.md.
/// </summary>
public sealed record ScorableOffer(string OfferId, decimal Price, TimeSpan Duration, int Stops, decimal Margin);

/// <summary>
/// Weights for <see cref="OfferScorer"/>. <see cref="MarginWeight"/> defaults to zero: margin is a
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

/// <summary>
/// Deterministic offer ranking. See docs/04-ranking.md for why this is a scoring function and not a
/// model call: ranking must be explainable, reproducible, and fast.
/// </summary>
public static class OfferScorer
{
    /// <summary>
    /// Lower score is better. Price, duration, and stops are costs, weighted positively; margin is a
    /// benefit, weighted negatively so a higher margin lowers (improves) the score once its weight is
    /// non-zero. Weights operate on each field's raw unit (currency, minutes, count) with no
    /// normalization between them — a caller who wants stops to matter as much as price has to choose
    /// a correspondingly larger stops weight, not rely on the fields being pre-scaled to comparable
    /// magnitudes.
    /// </summary>
    public static decimal Score(ScorableOffer offer, ScoringWeights weights) =>
        offer.Price * weights.PriceWeight
        + (decimal)offer.Duration.TotalMinutes * weights.DurationWeight
        + offer.Stops * weights.StopsWeight
        - offer.Margin * weights.MarginWeight;

    /// <summary>
    /// Ranks ascending by score (best first). Ties break by <see cref="ScorableOffer.OfferId"/>,
    /// ordinal ascending — arbitrary but deterministic, which is what matters.
    /// </summary>
    public static IReadOnlyList<ScorableOffer> Rank(IEnumerable<ScorableOffer> offers, ScoringWeights weights) =>
        [.. offers
            .Select(offer => (Offer: offer, Score: Score(offer, weights)))
            .OrderBy(x => x.Score)
            .ThenBy(x => x.Offer.OfferId, StringComparer.Ordinal)
            .Select(x => x.Offer)];
}

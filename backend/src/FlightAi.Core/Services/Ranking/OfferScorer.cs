using FlightAi.Core.Models.Ranking;

namespace FlightAi.Core.Services.Ranking;

/// <summary>
/// Deterministic offer ranking. See docs/reference/04-ranking.md for why this is a scoring function and not a
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

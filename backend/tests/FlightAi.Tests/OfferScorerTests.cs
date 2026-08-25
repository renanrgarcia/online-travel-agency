using FlightAi.Core.Models.Ranking;
using FlightAi.Core.Services.Ranking;
using Xunit;

namespace FlightAi.Tests;

/// <summary>
/// One test per eval in docs/specs/tasks/03-offer-scoring.md, against that task's fixed fixture set.
/// </summary>
public class OfferScorerTests
{
    // Fixture set from the task note. No single offer dominates on every axis.
    private static ScorableOffer Cheap(decimal margin = 80m) =>
        new("CHEAP", Price: 400m, Duration: TimeSpan.FromHours(11), Stops: 2, Margin: margin);

    private static ScorableOffer Fast(decimal margin = 20m) =>
        new("FAST", Price: 700m, Duration: TimeSpan.FromHours(6), Stops: 0, Margin: margin);

    private static ScorableOffer Mid(decimal margin = 50m) =>
        new("MID", Price: 550m, Duration: TimeSpan.FromHours(8), Stops: 1, Margin: margin);

    private static List<ScorableOffer> Fixtures(decimal cheapMargin = 80m, decimal fastMargin = 20m, decimal midMargin = 50m) =>
        [Cheap(cheapMargin), Fast(fastMargin), Mid(midMargin)];

    private static List<string> Ids(IReadOnlyList<ScorableOffer> ranked) => [.. ranked.Select(o => o.OfferId)];

    [Fact] // E1 — determinism is the property that justifies not using a model
    public void E1_RankingTheSameInputTwice_ProducesIdenticalOrder()
    {
        var offers = Fixtures();

        var first = Ids(OfferScorer.Rank(offers, ScoringWeights.Default));
        var second = Ids(OfferScorer.Rank(offers, ScoringWeights.Default));

        Assert.Equal(first, second);
    }

    [Fact] // E2 — a weight actually drives the outcome in the stated direction
    public void E2_PriceWeightDominant_RanksCheapestFirst()
    {
        var offers = Fixtures();
        var weights = new ScoringWeights(PriceWeight: 1m, DurationWeight: 0m, StopsWeight: 0m, MarginWeight: 0m);

        var ranked = Ids(OfferScorer.Rank(offers, weights));

        Assert.Equal(["CHEAP", "MID", "FAST"], ranked);
    }

    [Fact] // E3 — same point as E2, on an axis that orders the fixtures oppositely
    public void E3_DurationWeightDominant_RanksFastestFirst()
    {
        var offers = Fixtures();
        var weights = new ScoringWeights(PriceWeight: 0m, DurationWeight: 1m, StopsWeight: 0m, MarginWeight: 0m);

        var ranked = Ids(OfferScorer.Rank(offers, weights));

        Assert.Equal(["FAST", "MID", "CHEAP"], ranked);
    }

    [Fact] // E4 — margin has zero influence unless explicitly weighted, the point of the task
    public void E4_DefaultWeights_IgnoreMarginEntirely()
    {
        var withRealMargins = Fixtures(cheapMargin: 80m, fastMargin: 20m, midMargin: 50m);
        var withZeroMargins = Fixtures(cheapMargin: 0m, fastMargin: 0m, midMargin: 0m);

        var rankedWithMargins = Ids(OfferScorer.Rank(withRealMargins, ScoringWeights.Default));
        var rankedWithoutMargins = Ids(OfferScorer.Rank(withZeroMargins, ScoringWeights.Default));

        Assert.Equal(rankedWithMargins, rankedWithoutMargins);
    }

    [Fact] // E5 — proves the margin lever exists and works, so E4 is a real default, not a missing feature
    public void E5_MarginWeightExplicitlyNonZero_ChangesTheOrdering()
    {
        var offers = Fixtures();
        var defaultOrder = Ids(OfferScorer.Rank(offers, ScoringWeights.Default));

        var marginWeights = new ScoringWeights(PriceWeight: 0m, DurationWeight: 0m, StopsWeight: 0m, MarginWeight: 1m);
        var marginOrder = Ids(OfferScorer.Rank(offers, marginWeights));

        Assert.NotEqual(defaultOrder, marginOrder);
        Assert.Equal(["CHEAP", "MID", "FAST"], marginOrder); // highest margin (80) ranks first
    }

    [Fact] // E6 — ties must be deterministic too
    public void E6_OffersIdenticalOnEveryScoredField_BreakTiesByOfferIdAscending()
    {
        List<ScorableOffer> offers =
        [
            new("B", Price: 500m, Duration: TimeSpan.FromHours(5), Stops: 1, Margin: 0m),
            new("A", Price: 500m, Duration: TimeSpan.FromHours(5), Stops: 1, Margin: 0m),
        ];

        var ranked = Ids(OfferScorer.Rank(offers, ScoringWeights.Default));

        Assert.Equal(["A", "B"], ranked);
    }

    [Fact] // E7 — degenerate case pinned deliberately
    public void E7_EmptyOfferList_ReturnsEmptyResult()
    {
        var ranked = OfferScorer.Rank([], ScoringWeights.Default);

        Assert.Empty(ranked);
    }

    [Fact] // E8 — purity: no hidden state, no time-dependence, no culture-dependence
    public void E8_ScoringTheSameOfferTwice_ProducesIdenticalScore()
    {
        var offer = Mid();

        var first = OfferScorer.Score(offer, ScoringWeights.Default);
        var second = OfferScorer.Score(offer, ScoringWeights.Default);

        Assert.Equal(first, second);
    }
}

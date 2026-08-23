using FlightAi.Core.Offers;
using FlightAi.Core.Ranking;
using Xunit;

namespace FlightAi.Tests;

public class OfferScorerTests
{
    private static readonly Func<Offer[]> SampleOffers = () =>
    [
        TestOffers.Make("A", 800m, stopCount: 1, durationHours: 11),
        TestOffers.Make("B", 750m, stopCount: 1, durationHours: 12, refundable: true),
        TestOffers.Make("C", 900m, stopCount: 0, durationHours: 9)
    ];

    /// <summary>A test that proves ranking is deterministic across identical inputs.</summary>
    [Fact]
    public void Rank_IsDeterministic_AcrossRepeatedRuns()
    {
        var offers = SampleOffers();
        var scorer = new OfferScorer();

        var first = scorer.Rank(offers).Select(s => s.Offer.OfferId).ToList();
        var second = scorer.Rank(offers).Select(s => s.Offer.OfferId).ToList();

        Assert.Equal(first, second);
    }

    [Fact]
    public void Rank_IsIndependentOfInputOrder()
    {
        var offers = SampleOffers();
        var scorer = new OfferScorer();

        var forward = scorer.Rank(offers).Select(s => s.Offer.OfferId).ToList();
        var reversed = scorer.Rank(offers.Reverse().ToArray()).Select(s => s.Offer.OfferId).ToList();

        Assert.Equal(forward, reversed);
    }

    [Fact]
    public void CheapestOffer_WinsWhenEverythingElseIsEqual()
    {
        var cheap = TestOffers.Make("cheap", 500m);
        var expensive = TestOffers.Make("pricey", 900m);

        var ranked = new OfferScorer().Rank([cheap, expensive]);

        Assert.Equal("cheap", ranked[0].Offer.OfferId);
    }

    /// <summary>Margin must never sway ranking unless someone deliberately turns the weight on.</summary>
    [Fact]
    public void MarginWeight_DefaultsToZero_SoItNeverSwaysRankingByAccident()
    {
        var lowMargin = TestOffers.Make("low-margin", 700m, margin: 5m);
        var highMargin = TestOffers.Make("high-margin", 700m, margin: 500m);

        var ranked = new OfferScorer().Rank([lowMargin, highMargin]);

        Assert.Equal(ranked[0].Score, ranked[1].Score, precision: 9);
    }

    [Fact]
    public void MarginWeight_WhenDeliberatelyEnabled_DoesAffectRanking()
    {
        var lowMargin = TestOffers.Make("low-margin", 700m, margin: 5m);
        var highMargin = TestOffers.Make("high-margin", 700m, margin: 500m);
        var weights = ScoringWeights.Default with { Margin = 0.5 };

        var ranked = new OfferScorer(weights).Rank([lowMargin, highMargin]);

        Assert.Equal("high-margin", ranked[0].Offer.OfferId);
    }
}

using FlightAi.Core.Pricing;
using FlightAi.Core.Ranking;
using Xunit;

namespace FlightAi.Tests;

/// <summary>
/// These tests prove that a language model can never cause a raw number to reach the traveller,
/// whether by a resolved token or by typing digits directly into its prose — not just asserted in a
/// comment.
/// </summary>
public class ExplanationPlaceholderRendererTests
{
    private static PriceReferenceStore BuildStore()
    {
        var offers = new[]
        {
            TestOffers.Make("cheap-1", 500m, durationHours: 10, refundable: true),
            TestOffers.Make("pricey-1", 650m, stopCount: 1, durationHours: 8)
        };
        return new PriceReferenceStore(new OfferScorer().Rank(offers));
    }

    [Fact]
    public void WellFormedTokens_ResolveToRealValues_AndReportClean()
    {
        var store = BuildStore();
        var modelText = "The best option is {{PRICE_cheap-1}}, {{STOPS_cheap-1}}, {{REFUNDABLE_cheap-1}}.";

        var result = ExplanationPlaceholderRenderer.Render(modelText, store);

        Assert.True(result.IsClean);
        Assert.Contains("500.00 USD", result.Text);
        Assert.Contains("nonstop", result.Text);
        Assert.Contains("refundable", result.Text);
        Assert.DoesNotContain("{{", result.Text);
    }

    [Fact]
    public void ComparisonTokens_ResolveToSignedDeltas()
    {
        var store = BuildStore();
        var modelText = "The pricier option is {{PRICE_DELTA_pricey-1_vs_cheap-1}}.";

        var result = ExplanationPlaceholderRenderer.Render(modelText, store);

        Assert.True(result.IsClean);
        Assert.Contains("150.00 USD more", result.Text);
    }

    [Fact]
    public void HallucinatedToken_IsFlaggedAsUnresolved_NotSilentlyDropped()
    {
        var store = BuildStore();
        var result = ExplanationPlaceholderRenderer.Render("This flight costs {{PRICE_does-not-exist}}.", store);

        Assert.False(result.IsClean);
        Assert.Contains("PRICE_does-not-exist", result.UnresolvedTokens);
    }

    /// <summary>There is no MARGIN_ resolution path at all — not even a hallucinated reference can leak it.</summary>
    [Fact]
    public void MarginToken_HasNoResolutionPath_EvenIfAgentTriesToReferenceIt()
    {
        var store = BuildStore();
        var result = ExplanationPlaceholderRenderer.Render("Our margin here is {{MARGIN_cheap-1}}.", store);

        Assert.False(result.IsClean);
        Assert.Contains("MARGIN_cheap-1", result.UnresolvedTokens);
    }

    /// <summary>The failure mode this test exists to catch: the model ignores its instructions and just types a number.</summary>
    [Fact]
    public void ModelWritingARawNumberOutsideAnyToken_IsCaughtStructurally()
    {
        var store = BuildStore();
        var modelText = "This flight only costs $499, cheaper than the {{PRICE_cheap-1}} shown.";

        var result = ExplanationPlaceholderRenderer.Render(modelText, store);

        Assert.False(result.IsClean);
        Assert.True(result.HasStrayDigitsOutsideTokens);
    }

    [Fact]
    public void MalformedToken_LeavesVisibleBraces_RatherThanSilentlyPassingThrough()
    {
        var store = BuildStore();
        // A space inside the token is invalid — this must not silently match and must not vanish.
        var result = ExplanationPlaceholderRenderer.Render("Price: {{PRICE cheap-1}}.", store);

        Assert.False(result.IsClean);
        Assert.True(result.HasUnmatchedBraces);
        Assert.Contains("{{", result.Text);
    }
}

using FlightAi.Core.Pricing;
using Xunit;

namespace FlightAi.Tests;

public class PriceReferenceStoreTests
{
    [Fact]
    public void RegisterPrice_TokenDoesNotContainTheNumericPrice()
    {
        var store = new PriceReferenceStore();
        var token = store.RegisterPrice("OFF8812", 791.00m, "USD");

        Assert.DoesNotContain("791", token);
        Assert.Matches(@"^\{\{PRICE_OFF8812\}\}$", token);
    }

    [Fact]
    public void RegisterPrice_ResolvesBackToTheFormattedValue()
    {
        var store = new PriceReferenceStore();
        var token = store.RegisterPrice("OFF8812", 791.00m, "USD");

        Assert.True(store.TryResolve(token, out var value));
        Assert.Equal("$791.00", value);
    }

    [Fact]
    public void RegisterPrice_DifferentOffersProduceDifferentTokens()
    {
        var store = new PriceReferenceStore();
        var tokenA = store.RegisterPrice("OFFA", 500m, "USD");
        var tokenB = store.RegisterPrice("OFFB", 500m, "USD"); // same price, different offer

        Assert.NotEqual(tokenA, tokenB);
    }

    [Fact]
    public void RegisterPrice_TokenIdentityIsKeyedByOfferIdNotByPrice()
    {
        // Deliberate design decision: the token string is derived only from the offer ID, never
        // from the price value. If the price shaped the token itself, an observer could compare
        // token strings across offers and infer something about relative prices without ever
        // resolving them, which defeats the point of opacity. Re-registering the same offer ID
        // with a different price returns the identical token string; only the resolved value
        // changes underneath it.
        var store = new PriceReferenceStore();
        var firstToken = store.RegisterPrice("OFF8812", 100m, "USD");
        var secondToken = store.RegisterPrice("OFF8812", 999m, "USD");

        Assert.Equal(firstToken, secondToken);
        Assert.True(store.TryResolve(firstToken, out var value));
        Assert.Equal("$999.00", value); // last registration for a given offer ID wins
    }

    [Fact]
    public void TryResolve_UnknownToken_ReturnsFalse()
    {
        var store = new PriceReferenceStore();

        Assert.False(store.TryResolve("{{PRICE_NEVER_REGISTERED}}", out _));
    }

    [Fact]
    public void TryResolve_HallucinatedMarginToken_NeverResolves()
    {
        // There is deliberately no RegisterMargin method anywhere on this store: margin has no
        // token vocabulary at all, so a hallucinated {{MARGIN_OFF8812}} reference from a model can
        // never resolve -- by construction, not by a runtime check that could be forgotten.
        var store = new PriceReferenceStore();
        store.RegisterPrice("OFF8812", 791.00m, "USD");

        Assert.False(store.TryResolve("{{MARGIN_OFF8812}}", out _));
    }

    [Fact]
    public void RegisterDuration_FormatsHoursAndMinutes()
    {
        var store = new PriceReferenceStore();
        var token = store.RegisterDuration("OFF8812", TimeSpan.FromMinutes(330)); // 5h30m

        store.TryResolve(token, out var value);
        Assert.Equal("5h 30m", value);
    }

    [Fact]
    public void RegisterStops_UsesSingularAndPluralCorrectly()
    {
        var store = new PriceReferenceStore();

        store.TryResolve(store.RegisterStops("OFF1", 0), out var nonstop);
        store.TryResolve(store.RegisterStops("OFF2", 1), out var oneStop);
        store.TryResolve(store.RegisterStops("OFF3", 2), out var twoStops);

        Assert.Equal("nonstop", nonstop);
        Assert.Equal("1 stop", oneStop);
        Assert.Equal("2 stops", twoStops);
    }

    [Fact]
    public void RegisterRefundable_ReflectsTheFlag()
    {
        var store = new PriceReferenceStore();

        store.TryResolve(store.RegisterRefundable("OFF1", true), out var refundable);
        store.TryResolve(store.RegisterRefundable("OFF2", false), out var nonRefundable);

        Assert.Equal("refundable", refundable);
        Assert.Equal("non-refundable", nonRefundable);
    }

    [Fact]
    public void RegisterPriceDelta_ResolvesDirectionAndMagnitude()
    {
        var store = new PriceReferenceStore();

        store.TryResolve(store.RegisterPriceDelta("OFFA", "OFFB", 42.00m, "USD"), out var more);
        store.TryResolve(store.RegisterPriceDelta("OFFC", "OFFD", -15.00m, "USD"), out var less);
        store.TryResolve(store.RegisterPriceDelta("OFFE", "OFFF", 0m, "USD"), out var same);

        Assert.Equal("$42.00 more", more);
        Assert.Equal("$15.00 less", less);
        Assert.Equal("the same price", same);
    }

    [Fact]
    public void NoTokenStringEverContainsARawPriceDigitSequence()
    {
        // Sweeps every registration method with a shared, distinctive numeric value and confirms
        // none of the returned tokens contain it anywhere in their text -- this is the property
        // the whole store exists to guarantee, checked mechanically rather than by eye.
        var store = new PriceReferenceStore();
        var priceToken = store.RegisterPrice("OFF9001", 1234.56m, "USD");
        var deltaToken = store.RegisterPriceDelta("OFF9001", "OFF9002", 1234.56m, "USD");

        Assert.DoesNotContain("1234", priceToken);
        Assert.DoesNotContain("1234", deltaToken);
    }
}

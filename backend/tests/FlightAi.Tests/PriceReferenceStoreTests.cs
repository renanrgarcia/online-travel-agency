using System.Reflection;
using FlightAi.Core.Services.Pricing;
using Xunit;

namespace FlightAi.Tests;

/// <summary>
/// One test per eval in docs/specs/tasks/01-price-reference-tokens.md. Test names carry the eval ID
/// so a failure points straight at the acceptance criterion it violates. Expected values come from
/// that task note, which was written before this code existed — not from what the code happens to do.
/// </summary>
public class PriceReferenceStoreTests
{
    [Fact] // E1 — token spelling is fixed by docs/02-price-integrity.md
    public void E1_RegisterPrice_ReturnsTheDocumentedTokenSpelling()
    {
        var store = new PriceReferenceStore();

        Assert.Equal("{{PRICE_OFF8812}}", store.RegisterPrice("OFF8812", 791.00m, "USD"));
    }

    [Fact] // E2 — the opacity property the store exists to provide
    public void E2_PriceToken_ContainsNoTraceOfTheNumericValue()
    {
        var store = new PriceReferenceStore();
        var token = store.RegisterPrice("OFF8812", 791.00m, "USD");

        Assert.DoesNotContain("791", token);
        Assert.DoesNotContain("791.00", token);
        Assert.DoesNotContain("$791", token);
    }

    [Fact] // E3 — resolution fidelity
    public void E3_TryResolve_ReturnsTheFormattedValue()
    {
        var store = new PriceReferenceStore();
        var token = store.RegisterPrice("OFF8812", 791.00m, "USD");

        Assert.True(store.TryResolve(token, out var value));
        Assert.Equal("$791.00", value);
    }

    [Fact] // E4 — identical prices must not collapse to one token
    public void E4_DifferentOffersWithTheSamePrice_GetDifferentTokens()
    {
        var store = new PriceReferenceStore();

        var tokenA = store.RegisterPrice("OFFA", 500m, "USD");
        var tokenB = store.RegisterPrice("OFFB", 500m, "USD");

        Assert.NotEqual(tokenA, tokenB);
    }

    [Fact] // E5 — token identity is keyed by offer ID alone; last write wins
    public void E5_ReRegisteringAnOffer_ReturnsTheSameTokenAndOverwritesTheValue()
    {
        var store = new PriceReferenceStore();

        var first = store.RegisterPrice("OFF1", 100m, "USD");
        var second = store.RegisterPrice("OFF1", 999m, "USD");

        Assert.Equal(first, second);
        Assert.True(store.TryResolve(first, out var value));
        Assert.Equal("$999.00", value);
    }

    [Fact] // E6 — unknown tokens must fail rather than resolve to something plausible
    public void E6_TryResolve_UnknownToken_ReturnsFalse()
    {
        var store = new PriceReferenceStore();

        Assert.False(store.TryResolve("{{PRICE_NEVER_ISSUED}}", out _));
    }

    [Fact] // E7 — a hallucinated margin reference must never resolve
    public void E7_TryResolve_MarginToken_ReturnsFalse()
    {
        var store = new PriceReferenceStore();
        store.RegisterPrice("OFF8812", 791.00m, "USD");

        Assert.False(store.TryResolve("{{MARGIN_OFF8812}}", out _));
    }

    [Fact] // E8 — stronger than E7: margin is absent by construction, not blocked by a check
    public void E8_PublicApi_ExposesNoMarginMemberAtAll()
    {
        var members = typeof(PriceReferenceStore)
            .GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);

        Assert.DoesNotContain(members, m => m.Name.Contains("Margin", StringComparison.OrdinalIgnoreCase));
    }

    [Fact] // E9 — duration display format locked for tasks 02 and 11
    public void E9_RegisterDuration_FormatsHoursAndMinutes()
    {
        var store = new PriceReferenceStore();
        store.TryResolve(store.RegisterDuration("OFF1", TimeSpan.FromMinutes(330)), out var value);

        Assert.Equal("5h 30m", value);
    }

    [Fact] // E10 — exact-hour case stated explicitly rather than left to implementation
    public void E10_RegisterDuration_ExactHours_OmitsTheMinutesPart()
    {
        var store = new PriceReferenceStore();
        store.TryResolve(store.RegisterDuration("OFF1", TimeSpan.FromMinutes(120)), out var value);

        Assert.Equal("2h", value);
    }

    [Theory] // E11 — pluralisation travellers notice
    [InlineData(0, "nonstop")]
    [InlineData(1, "1 stop")]
    [InlineData(2, "2 stops")]
    public void E11_RegisterStops_PluralisesCorrectly(int stops, string expected)
    {
        var store = new PriceReferenceStore();
        store.TryResolve(store.RegisterStops("OFF1", stops), out var value);

        Assert.Equal(expected, value);
    }

    [Theory] // E12
    [InlineData(true, "refundable")]
    [InlineData(false, "non-refundable")]
    public void E12_RegisterRefundable_ReflectsTheFlag(bool refundable, string expected)
    {
        var store = new PriceReferenceStore();
        store.TryResolve(store.RegisterRefundable("OFF1", refundable), out var value);

        Assert.Equal(expected, value);
    }

    [Fact] // E13 — delta token spelling fixed by source doc; positive means B costs more
    public void E13_RegisterPriceDelta_PositiveDelta_ReadsAsMore()
    {
        var store = new PriceReferenceStore();
        var token = store.RegisterPriceDelta("OFFA", "OFFB", 42.00m, "USD");

        Assert.Equal("{{PRICE_DELTA_OFFA_vs_OFFB}}", token);
        store.TryResolve(token, out var value);
        Assert.Equal("$42.00 more", value);
    }

    [Fact] // E14 — negative renders as magnitude + direction, never a minus sign in prose
    public void E14_RegisterPriceDelta_NegativeDelta_ReadsAsLess()
    {
        var store = new PriceReferenceStore();
        store.TryResolve(store.RegisterPriceDelta("OFFA", "OFFB", -15.00m, "USD"), out var value);

        Assert.Equal("$15.00 less", value);
    }

    [Fact] // E15 — zero is a distinct case, not "$0.00 more"
    public void E15_RegisterPriceDelta_ZeroDelta_ReadsAsTheSamePrice()
    {
        var store = new PriceReferenceStore();
        store.TryResolve(store.RegisterPriceDelta("OFFA", "OFFB", 0m, "USD"), out var value);

        Assert.Equal("the same price", value);
    }

    [Fact] // E16 — sweep of E2 across the whole registering API surface
    public void E16_NoRegistrationMethod_LeaksItsValueIntoTheTokenText()
    {
        var store = new PriceReferenceStore();

        var tokens = new[]
        {
            store.RegisterPrice("OFF9001", 1234.56m, "USD"),
            store.RegisterPriceDelta("OFF9001", "OFF9002", 1234.56m, "USD"),
            store.RegisterDuration("OFF9001", TimeSpan.FromMinutes(1234)),
            store.RegisterStops("OFF9001", 1234),
        };

        Assert.All(tokens, token => Assert.DoesNotContain("1234", token));
    }
}

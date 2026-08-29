using FlightAi.Core.Services.Pricing;
using Xunit;

namespace FlightAi.Tests;

/// <summary>
/// One test per eval in docs/features/01-backend/tasks/02-explanation-placeholder-renderer.md. Read together with
/// PriceReferenceStoreTests as one unit, per docs/reference/02-price-integrity.md.
/// </summary>
public class ExplanationPlaceholderRendererTests
{
    private static (PriceReferenceStore Store, ExplanationPlaceholderRenderer Renderer) NewRenderer()
    {
        var store = new PriceReferenceStore();
        return (store, new ExplanationPlaceholderRenderer(store));
    }

    [Fact] // E1 — the happy path: tokens resolve, surrounding words untouched
    public void E1_HappyPath_ResolvesTokensAndLeavesSurroundingTextUntouched()
    {
        var (store, renderer) = NewRenderer();
        store.RegisterPrice("OFF1", 791.00m, "USD");
        store.RegisterDuration("OFF1", TimeSpan.FromMinutes(330));

        var result = renderer.Render("This option is {{PRICE_OFF1}} and takes {{DURATION_OFF1}}.");

        Assert.True(result.Success);
        Assert.Equal("This option is $791.00 and takes 5h 30m.", result.Text);
        Assert.Empty(result.Violations);
    }

    [Fact] // E2 — an unrecognised reference fails loudly, never vanishes
    public void E2_UnknownToken_LeftVisiblyUnresolved()
    {
        var (_, renderer) = NewRenderer();

        var result = renderer.Render("Costs {{PRICE_UNKNOWN}}.");

        Assert.False(result.Success);
        Assert.Contains("{{PRICE_UNKNOWN}}", result.Text);
        Assert.Contains("{{PRICE_UNKNOWN}}", result.UnresolvedTokens);
    }

    [Fact] // E3 — the core failure mode: a model typing a number instead of using a token
    public void E3_RawDigitWithNoTokenAtAll_RejectedAsAViolation()
    {
        var (_, renderer) = NewRenderer();

        var result = renderer.Render("A great deal at $999.");

        Assert.False(result.Success);
        Assert.NotEmpty(result.Violations);
    }

    [Fact] // E4 — any digit outside a token is a violation, including a model's own comparison math
    public void E4_DigitOutsideToken_RejectedEvenAlongsideAValidToken()
    {
        var (store, renderer) = NewRenderer();
        store.RegisterPrice("OFF1", 500m, "USD");

        var result = renderer.Render("This is {{PRICE_OFF1}}, about 20% cheaper.");

        Assert.False(result.Success);
        Assert.NotEmpty(result.Violations);
    }

    [Fact] // E5 — the scan runs on raw input, not on the rendered result
    public void E5_ResolvedOutputWithNoDigits_NoViolation()
    {
        var (store, renderer) = NewRenderer();
        store.RegisterStops("OFF1", 0);
        store.RegisterRefundable("OFF1", true);

        var result = renderer.Render("Only {{STOPS_OFF1}} and it is {{REFUNDABLE_OFF1}}.");

        Assert.True(result.Success);
        Assert.Equal("Only nonstop and it is refundable.", result.Text);
    }

    [Fact] // E6 — rendering legitimately introduces digits; that must not trip the guard
    public void E6_ResolvedOutputContainingDigits_StillNoViolation()
    {
        var (store, renderer) = NewRenderer();
        store.RegisterDuration("OFF1", TimeSpan.FromMinutes(330));

        var result = renderer.Render("It is {{DURATION_OFF1}}.");

        Assert.True(result.Success);
        Assert.Equal("It is 5h 30m.", result.Text);
        Assert.Empty(result.Violations);
    }

    [Fact] // E7 — margin is unreachable end to end, not merely unregistered
    public void E7_MarginToken_NeverResolves()
    {
        var (_, renderer) = NewRenderer();

        var result = renderer.Render("Try {{MARGIN_OFF1}} today.");

        Assert.False(result.Success);
        Assert.Contains("{{MARGIN_OFF1}}", result.UnresolvedTokens);
    }

    [Fact] // E8 — degenerate case pinned deliberately
    public void E8_EmptyInput_SucceedsWithEmptyOutput()
    {
        var (_, renderer) = NewRenderer();

        var result = renderer.Render("");

        Assert.True(result.Success);
        Assert.Equal("", result.Text);
        Assert.Empty(result.Violations);
    }

    [Fact] // E9 — token boundary detection doesn't depend on surrounding whitespace
    public void E9_AdjacentTokensWithNoSeparator_BothResolve()
    {
        var (store, renderer) = NewRenderer();
        store.RegisterPrice("OFF1", 100m, "USD");
        store.RegisterPrice("OFF2", 200m, "USD");

        var result = renderer.Render("{{PRICE_OFF1}}{{PRICE_OFF2}}");

        Assert.True(result.Success);
        Assert.Equal("$100.00$200.00", result.Text);
    }

    [Fact] // E10 — a failed check has to be diagnosable, not just a boolean
    public void E10_ViolationResult_CarriesTheOffendingSubstring()
    {
        var (_, renderer) = NewRenderer();

        var result = renderer.Render("A great deal at $999.");

        Assert.Contains(result.Violations, v => v.Contains("999"));
    }

    [Fact] // E11 — closes the digit-scan's blind spot for a model spelling numbers out in English
    public void E11_SpelledOutEnglishNumber_RejectedAsAViolation()
    {
        var (_, renderer) = NewRenderer();

        var result = renderer.Render("It costs about seven hundred ninety-one dollars for the trip.");

        Assert.False(result.Success);
        Assert.Contains(result.Violations, v => v.Contains("hundred"));
    }

    [Fact] // E12 — the target market is Brazilian; an English-only guard would be a product bug
    public void E12_SpelledOutPortugueseNumber_RejectedAsAViolation()
    {
        var (_, renderer) = NewRenderer();

        var result = renderer.Render("Custa cerca de setecentos reais para a viagem.");

        Assert.False(result.Success);
        Assert.NotEmpty(result.Violations);
    }

    [Fact] // Locked decision: "one"/"um" style pronouns must not false-positive
    public void PronounNumberWords_DoNotTriggerTheGuard()
    {
        var (_, renderer) = NewRenderer();

        var result = renderer.Render("This is the only one option, and um momento of your time.");

        Assert.True(result.Success);
    }
}

using FlightAi.Agents.Models.Explanation;
using FlightAi.Agents.Services;
using FlightAi.Agents.Services.Explanation;
using FlightAi.Core.Models.Offers;
using FlightAi.Core.Services.Pricing;
using Xunit;

namespace FlightAi.Tests;

/// <summary>
/// One test per eval in docs/features/01-backend/tasks/18-comparative-decision-support.md, spanning
/// PriceReferenceStore's new registrations and ComparisonFacts' decision logic (FlightAi.Core), plus
/// the explanation agent's end-to-end behaviour (FlightAi.Agents) -- one cohesive task tested together,
/// the same way SearchApiPipelineTests already spans multiple components for task 13.
/// </summary>
public class ComparativeDecisionSupportTests
{
    private static Offer MakeOffer(
        string offerId, decimal price, TimeSpan duration, int stops = 0, bool refundable = false, string currency = "USD") =>
        new(offerId, price, currency, duration, stops, refundable, Margin: 0m, ExpiresAt: DateTimeOffset.UtcNow.AddHours(1));

    [Fact] // E1 — the comparison is code-authored, exactly like the number is
    public void E1_PriceDelta_ResolvesToMoreOrLessByDirection()
    {
        var top = MakeOffer("A", 590.00m, TimeSpan.FromHours(8));
        var cheaper = MakeOffer("B", 410.00m, TimeSpan.FromHours(8));

        var store = new PriceReferenceStore();
        var comparisons = ComparisonFacts.Compute(store, [top, cheaper]);
        store.TryResolve(comparisons["B"].PriceDeltaToken!, out var resolved);
        Assert.Equal("$180.00 less", resolved);

        // The inverse: same two offers, B ranked first this time.
        var inverseStore = new PriceReferenceStore();
        var inverseComparisons = ComparisonFacts.Compute(inverseStore, [cheaper, top]);
        inverseStore.TryResolve(inverseComparisons["A"].PriceDeltaToken!, out var inverseResolved);
        Assert.Equal("$180.00 more", inverseResolved);
    }

    [Fact] // E2 — second dimension, same mechanism
    public void E2_DurationDelta_ResolvesToShorterLongerOrTheSame()
    {
        var top = MakeOffer("A", 500m, TimeSpan.FromHours(8));
        var longer = MakeOffer("B", 500m, TimeSpan.FromHours(11));
        var same = MakeOffer("C", 500m, TimeSpan.FromHours(8));

        var store = new PriceReferenceStore();
        var comparisons = ComparisonFacts.Compute(store, [top, longer, same]);

        store.TryResolve(comparisons["B"].DurationDeltaToken!, out var longerResolved);
        Assert.Equal("3h longer", longerResolved);
        store.TryResolve(comparisons["C"].DurationDeltaToken!, out var sameResolved);
        Assert.Equal("the same duration", sameResolved);

        var inverseStore = new PriceReferenceStore();
        var inverseComparisons = ComparisonFacts.Compute(inverseStore, [longer, top]);
        inverseStore.TryResolve(inverseComparisons["A"].DurationDeltaToken!, out var shorterResolved);
        Assert.Equal("3h shorter", shorterResolved);
    }

    [Fact] // E3 — each superlative resolves only for the offer that actually holds it
    public void E3_Superlatives_ResolveOnlyForTheOfferThatActuallyHoldsThem()
    {
        // Distinct holders by design: A is cheapest, B is fastest, C has fewest stops and is the only
        // refundable one -- and A/B each hold exactly one superlative, not several, to keep this specific
        // eval about "different offer, different fact" rather than the multi-superlative case (covered below).
        var cheapest = MakeOffer("A", 400m, TimeSpan.FromHours(9), stops: 2, refundable: false);
        var fastest = MakeOffer("B", 600m, TimeSpan.FromHours(4), stops: 2, refundable: false);
        var fewestStopsAndRefundable = MakeOffer("C", 500m, TimeSpan.FromHours(6), stops: 0, refundable: true);

        var store = new PriceReferenceStore();
        var comparisons = ComparisonFacts.Compute(store, [cheapest, fastest, fewestStopsAndRefundable]);

        Assert.Single(comparisons["A"].SuperlativeTokens);
        store.TryResolve(comparisons["A"].SuperlativeTokens[0], out var aText);
        Assert.Equal("the cheapest option", aText);

        Assert.Single(comparisons["B"].SuperlativeTokens);
        store.TryResolve(comparisons["B"].SuperlativeTokens[0], out var bText);
        Assert.Equal("the fastest option", bText);

        // C holds two distinct superlatives at once -- both must survive, not just one.
        Assert.Equal(2, comparisons["C"].SuperlativeTokens.Count);
        var cTexts = comparisons["C"].SuperlativeTokens.Select(token => store.TryResolve(token, out var text) ? text : null).ToList();
        Assert.Contains("the option with the fewest stops", cTexts);
        Assert.Contains("the only refundable option", cTexts);
    }

    [Fact] // E3 (tie case) — a tie means no offer can honestly claim the superlative
    public void E3_TiedMinimum_RegistersNoSuperlativeForEitherOffer()
    {
        var tiedA = MakeOffer("A", 500m, TimeSpan.FromHours(8));
        var tiedB = MakeOffer("B", 500m, TimeSpan.FromHours(6));

        var store = new PriceReferenceStore();
        var comparisons = ComparisonFacts.Compute(store, [tiedA, tiedB]);

        Assert.DoesNotContain(comparisons["A"].SuperlativeTokens, token => token.Contains("CHEAPEST"));
        Assert.DoesNotContain(comparisons["B"].SuperlativeTokens, token => token.Contains("CHEAPEST"));
    }

    [Fact] // E4 — don't state facts about offers the traveller isn't being shown
    public void E4_OnlyOffersPassedIn_EverReceiveComparisonFacts()
    {
        // Six ranked offers exist in principle; only the top three are ever handed to ComparisonFacts,
        // mirroring exactly how SearchPipeline calls it (ranked.Take(ExplainedOfferCount)).
        var explained = new[]
        {
            MakeOffer("RANK1", 400m, TimeSpan.FromHours(8)),
            MakeOffer("RANK2", 500m, TimeSpan.FromHours(6)),
            MakeOffer("RANK3", 600m, TimeSpan.FromHours(7)),
        };

        var store = new PriceReferenceStore();
        var comparisons = ComparisonFacts.Compute(store, explained);

        Assert.Equal(3, comparisons.Count);
        Assert.All(comparisons.Keys, key => Assert.Contains(key, new[] { "RANK1", "RANK2", "RANK3" }));
    }

    [Fact] // E9 — determinism, the same property ranking already has
    public void E9_SameOffersSameLanguageTwice_ProducesByteIdenticalComparisonFacts()
    {
        Offer[] Offers() =>
        [
            MakeOffer("A", 590m, TimeSpan.FromHours(8)),
            MakeOffer("B", 410m, TimeSpan.FromHours(11), stops: 1, refundable: true),
        ];

        var firstStore = new PriceReferenceStore();
        var first = ComparisonFacts.Compute(firstStore, Offers());
        var secondStore = new PriceReferenceStore();
        var second = ComparisonFacts.Compute(secondStore, Offers());

        firstStore.TryResolve(first["B"].PriceDeltaToken!, out var firstPrice);
        secondStore.TryResolve(second["B"].PriceDeltaToken!, out var secondPrice);
        Assert.Equal(firstPrice, secondPrice);

        firstStore.TryResolve(first["B"].DurationDeltaToken!, out var firstDuration);
        secondStore.TryResolve(second["B"].DurationDeltaToken!, out var secondDuration);
        Assert.Equal(firstDuration, secondDuration);

        Assert.Equal(first["B"].SuperlativeTokens.Count, second["B"].SuperlativeTokens.Count);
    }

    // --- End-to-end through the explanation agent (E5, E6, E7, E8, E10) ---

    private static TokenizedOffer TokenizeWithComparison(
        PriceReferenceStore store, Offer offer, IReadOnlyDictionary<string, OfferComparison> comparisons) =>
        new(
            offer.OfferId,
            store.RegisterPrice(offer.OfferId, offer.Price, offer.Currency),
            store.RegisterDuration(offer.OfferId, offer.Duration),
            store.RegisterStops(offer.OfferId, offer.Stops),
            store.RegisterRefundable(offer.OfferId, offer.Refundable),
            comparisons[offer.OfferId].PriceDeltaToken,
            comparisons[offer.OfferId].DurationDeltaToken,
            comparisons[offer.OfferId].SuperlativeTokens);

    [Fact] // E5 — the comparison mechanism must not itself become the leak it was built to prevent
    public async Task E5_PromptSentToTheChatClient_ContainsNoRawPriceOrDurationDigits()
    {
        var top = MakeOffer("OFF1", 590m, TimeSpan.FromHours(8));
        var cheaper = MakeOffer("OFF2", 410m, TimeSpan.FromHours(11));
        var store = new PriceReferenceStore();
        var comparisons = ComparisonFacts.Compute(store, [top, cheaper]);
        var offers = new[] { top, cheaper }.Select(o => TokenizeWithComparison(store, o, comparisons)).ToList();

        var client = new OfflineChatClient().RegisterResponse("Offer OFF1", "any response works here");
        var agent = ExplanationAgentFactory.Create(client, "en");

        await agent.ExplainAsync(offers);

        Assert.NotNull(client.LastUserPrompt);
        Assert.DoesNotContain("590", client.LastUserPrompt);
        Assert.DoesNotContain("410", client.LastUserPrompt);
        Assert.DoesNotContain("180", client.LastUserPrompt); // the delta magnitude itself
        Assert.Contains(offers[1].PriceDeltaToken!, client.LastUserPrompt);
    }

    [Fact] // E6 — end to end through the price-integrity boundary
    public async Task E6_ModelUsingDeltaAndSuperlativeTokens_RendersCleanWithEveryTokenResolved()
    {
        var top = MakeOffer("OFF1", 590m, TimeSpan.FromHours(8));
        var cheaper = MakeOffer("OFF2", 410m, TimeSpan.FromHours(11));
        var store = new PriceReferenceStore();
        var comparisons = ComparisonFacts.Compute(store, [top, cheaper]);
        var offers = new[] { top, cheaper }.Select(o => TokenizeWithComparison(store, o, comparisons)).ToList();

        var canned = $"The top pick is {offers[0].PriceToken}. A cheaper option is {offers[1].PriceDeltaToken}, " +
            $"but it takes {offers[1].DurationDeltaToken}.";
        var client = new OfflineChatClient().RegisterResponse("Offer OFF1", canned);
        var agent = ExplanationAgentFactory.Create(client, "en");

        var prose = await agent.ExplainAsync(offers);
        var rendered = new ExplanationPlaceholderRenderer(store).Render(prose);

        Assert.True(rendered.Success);
        Assert.Empty(rendered.UnresolvedTokens);
        Assert.Empty(rendered.Violations);
        Assert.Equal("The top pick is $590.00. A cheaper option is $180.00 less, but it takes 3h longer.", rendered.Text);
    }

    [Fact] // E7 — the existing guard still catches the obvious failure
    public async Task E7_ModelInventingAPercentageInsteadOfUsingADeltaToken_IsStillRejected()
    {
        var top = MakeOffer("OFF1", 590m, TimeSpan.FromHours(8));
        var cheaper = MakeOffer("OFF2", 410m, TimeSpan.FromHours(11));
        var store = new PriceReferenceStore();
        var comparisons = ComparisonFacts.Compute(store, [top, cheaper]);
        var offers = new[] { top, cheaper }.Select(o => TokenizeWithComparison(store, o, comparisons)).ToList();

        var client = new OfflineChatClient().RegisterResponse("Offer OFF1", "The other option is about 20% cheaper.");
        var agent = ExplanationAgentFactory.Create(client, "en");

        var prose = await agent.ExplainAsync(offers);
        var rendered = new ExplanationPlaceholderRenderer(store).Render(prose);

        Assert.False(rendered.Success);
        Assert.NotEmpty(rendered.Violations);
    }

    [Fact] // E8 — the target market; today this silently fails without the fix
    public async Task E8_PortugueseRequest_EveryResolvedTokenTextIsPortuguese()
    {
        var top = MakeOffer("OFF1", 590m, TimeSpan.FromHours(8), stops: 0, refundable: true);
        var cheaper = MakeOffer("OFF2", 410m, TimeSpan.FromHours(11));
        var store = new PriceReferenceStore("pt-BR");
        var comparisons = ComparisonFacts.Compute(store, [top, cheaper]);
        var offers = new[] { top, cheaper }.Select(o => TokenizeWithComparison(store, o, comparisons)).ToList();

        var canned = $"A melhor opção é {offers[0].PriceToken}, {offers[0].StopsToken}, {offers[0].RefundableToken}. " +
            $"Uma opção mais barata é {offers[1].PriceDeltaToken}.";
        var client = new OfflineChatClient().RegisterResponse("Offer OFF1", canned);
        var agent = ExplanationAgentFactory.Create(client, "pt-BR");

        var prose = await agent.ExplainAsync(offers);
        var rendered = new ExplanationPlaceholderRenderer(store).Render(prose);

        Assert.True(rendered.Success);
        Assert.Empty(rendered.Violations);
        Assert.Contains("sem escalas", rendered.Text);
        Assert.Contains("reembolsável", rendered.Text);
        Assert.Contains("a menos", rendered.Text);
        Assert.DoesNotContain("nonstop", rendered.Text);
        Assert.DoesNotContain("non-refundable", rendered.Text);
        Assert.DoesNotContain(" less", rendered.Text);
    }

    [Fact] // E10 — documents exactly where the structural guarantee stops
    public async Task E10_ModelStatingAComparisonWithNoTokenAtAll_IsNotCaught()
    {
        var top = MakeOffer("OFF1", 590m, TimeSpan.FromHours(8));
        var cheaper = MakeOffer("OFF2", 410m, TimeSpan.FromHours(11));
        var store = new PriceReferenceStore();
        var comparisons = ComparisonFacts.Compute(store, [top, cheaper]);
        var offers = new[] { top, cheaper }.Select(o => TokenizeWithComparison(store, o, comparisons)).ToList();

        // No digit, no token -- a bare comparative claim the model was never given any basis for.
        var client = new OfflineChatClient().RegisterResponse("Offer OFF1", "The other option is cheaper, but it's faster.");
        var agent = ExplanationAgentFactory.Create(client, "en");

        var prose = await agent.ExplainAsync(offers);
        var rendered = new ExplanationPlaceholderRenderer(store).Render(prose);

        // This is the documented limitation, not a bug: a comparative adjective with no digit and no
        // token is invisible to the guard, whether or not it happens to be true.
        Assert.True(rendered.Success);
        Assert.Empty(rendered.Violations);
    }
}

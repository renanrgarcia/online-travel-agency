using System.Reflection;
using FlightAi.Agents.Models.Explanation;
using FlightAi.Agents.Services;
using FlightAi.Agents.Services.Explanation;
using FlightAi.Core.Services.Pricing;
using Xunit;

namespace FlightAi.Tests;

/// <summary>
/// One test per eval in docs/features/01-backend/tasks/11-explanation-agent.md. Read together with
/// ExplanationPlaceholderRendererTests as one unit, per docs/reference/02-price-integrity.md.
/// </summary>
public class ExplanationAgentFactoryTests
{
    private static (PriceReferenceStore Store, IReadOnlyList<TokenizedOffer> Offers) ThreeTokenizedOffers()
    {
        var store = new PriceReferenceStore();
        List<TokenizedOffer> offers =
        [
            Tokenize(store, "OFF1", 500m, TimeSpan.FromHours(5), 0, true),
            Tokenize(store, "OFF2", 650m, TimeSpan.FromHours(7), 1, false),
            Tokenize(store, "OFF3", 800m, TimeSpan.FromHours(4), 0, true),
        ];
        return (store, offers);
    }

    private static TokenizedOffer Tokenize(PriceReferenceStore store, string offerId, decimal price, TimeSpan duration, int stops, bool refundable) =>
        new(
            offerId,
            store.RegisterPrice(offerId, price, "USD"),
            store.RegisterDuration(offerId, duration),
            store.RegisterStops(offerId, stops),
            store.RegisterRefundable(offerId, refundable));

    [Fact] // E1 — the happy path across the whole boundary
    public async Task E1_WellBehavedResponse_ResolvesCleanlyWithNoUnresolvedTokens()
    {
        var (store, offers) = ThreeTokenizedOffers();
        var canned = $"The best deal is {offers[0].PriceToken}, taking {offers[0].DurationToken}, " +
            $"{offers[0].StopsToken}, and it is {offers[0].RefundableToken}. A pricier option is {offers[1].PriceToken}.";
        var client = new OfflineChatClient().RegisterResponse("Offer OFF1", canned);
        var agent = ExplanationAgentFactory.Create(client, "en");

        var prose = await agent.ExplainAsync(offers);
        var rendered = new ExplanationPlaceholderRenderer(store).Render(prose);

        Assert.True(rendered.Success);
        Assert.Empty(rendered.UnresolvedTokens);
        Assert.Empty(rendered.Violations);
    }

    [Fact] // E2 — the agent cannot leak what it was never given, verified on the prompt
    public async Task E2_PromptSentToTheChatClient_ContainsNoRawOfferValues()
    {
        var (_, offers) = ThreeTokenizedOffers();
        var client = new OfflineChatClient().RegisterResponse("Offer OFF1", "any response works here");
        var agent = ExplanationAgentFactory.Create(client, "en");

        await agent.ExplainAsync(offers);

        Assert.NotNull(client.LastUserPrompt);
        Assert.DoesNotContain("500", client.LastUserPrompt); // OFF1's real price
        Assert.DoesNotContain("650", client.LastUserPrompt); // OFF2's real price
        Assert.DoesNotContain("800", client.LastUserPrompt); // OFF3's real price
        Assert.Contains(offers[0].PriceToken, client.LastUserPrompt);
    }

    [Fact] // E3 — the safety net sits in front of this agent for real, not merely in task 02's isolated tests
    public async Task E3_MisbehavingResponse_IsRejectedByTheRendererGuard()
    {
        var (store, offers) = ThreeTokenizedOffers();
        var client = new OfflineChatClient().RegisterResponse("Offer OFF1", $"The best deal is {offers[0].PriceToken}, only $999 today!");
        var agent = ExplanationAgentFactory.Create(client, "en");

        var prose = await agent.ExplainAsync(offers);
        var rendered = new ExplanationPlaceholderRenderer(store).Render(prose);

        Assert.False(rendered.Success);
        Assert.NotEmpty(rendered.Violations);
    }

    [Fact] // E4 — margin unreachable end to end
    public async Task E4_HallucinatedMarginToken_NeverResolves()
    {
        var (store, offers) = ThreeTokenizedOffers();
        var client = new OfflineChatClient().RegisterResponse("Offer OFF1", "This deal includes a hidden {{MARGIN_OFF1}} bonus!");
        var agent = ExplanationAgentFactory.Create(client, "en");

        var prose = await agent.ExplainAsync(offers);
        var rendered = new ExplanationPlaceholderRenderer(store).Render(prose);

        Assert.False(rendered.Success);
        Assert.Contains("{{MARGIN_OFF1}}", rendered.UnresolvedTokens);
    }

    [Fact] // E5 — the agent has no capability to resolve, structurally, not merely a convention it follows
    public void E5_ExplanationAgent_HoldsNoReferenceToPriceReferenceStore()
    {
        var fields = typeof(ExplanationAgent).GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);

        Assert.DoesNotContain(fields, f => f.FieldType == typeof(PriceReferenceStore));
    }

    [Fact] // E6 — determinism through the second AI touchpoint
    public async Task E6_SameOffersTwice_ProducesIdenticalProse()
    {
        var (_, offers) = ThreeTokenizedOffers();
        var client = new OfflineChatClient().RegisterResponse("Offer OFF1", "deterministic response text");
        var agent = ExplanationAgentFactory.Create(client, "en");

        var first = await agent.ExplainAsync(offers);
        var second = await agent.ExplainAsync(offers);

        Assert.Equal(first, second);
    }

    [Fact] // E7 — the numbers a traveller sees came from deterministic code, the whole thesis
    public async Task E7_RenderedOutput_ContainsThePriceTheStoreActuallyRegistered()
    {
        var (store, offers) = ThreeTokenizedOffers(); // OFF1 registered at 500.00 USD
        var client = new OfflineChatClient().RegisterResponse("Offer OFF1", $"Book now for {offers[0].PriceToken}!");
        var agent = ExplanationAgentFactory.Create(client, "en");

        var prose = await agent.ExplainAsync(offers);
        var rendered = new ExplanationPlaceholderRenderer(store).Render(prose);

        Assert.Equal("Book now for $500.00!", rendered.Text);
    }

    [Fact] // E8 — the bilingual requirement made concrete at the output side
    public async Task E8_PortugueseLanguage_InstructsTheAgentToRespondInPortuguese()
    {
        var (_, offers) = ThreeTokenizedOffers();
        var client = new OfflineChatClient().RegisterResponse("Offer OFF1", "resposta em português");
        var agent = ExplanationAgentFactory.Create(client, "pt-BR");

        await agent.ExplainAsync(offers);

        Assert.Contains("Portuguese", client.LastInstructions);
    }

    [Fact] // E9 — the same mechanism working both directions, not just defaulting to Portuguese
    public async Task E9_EnglishLanguage_InstructsTheAgentToRespondInEnglish()
    {
        var (_, offers) = ThreeTokenizedOffers();
        var client = new OfflineChatClient().RegisterResponse("Offer OFF1", "response in english");
        var agent = ExplanationAgentFactory.Create(client, "en");

        await agent.ExplainAsync(offers);

        Assert.Contains("English", client.LastInstructions);
        Assert.DoesNotContain("Portuguese", client.LastInstructions);
    }

    [Fact] // E10 — confirms task 02 E12's Portuguese magnitude-word check actually protects this path
    public async Task E10_PortugueseResponse_StillPassesTheDigitAndWordGuard()
    {
        var (store, offers) = ThreeTokenizedOffers();
        var client = new OfflineChatClient().RegisterResponse("Offer OFF1", $"A melhor oferta é {offers[0].PriceToken}, com {offers[0].StopsToken}.");
        var agent = ExplanationAgentFactory.Create(client, "pt-BR");

        var prose = await agent.ExplainAsync(offers);
        var rendered = new ExplanationPlaceholderRenderer(store).Render(prose);

        Assert.True(rendered.Success);
        Assert.Empty(rendered.Violations);
    }
}

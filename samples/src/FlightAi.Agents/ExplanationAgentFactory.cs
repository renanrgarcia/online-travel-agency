using System.Text;
using System.Text.Json;
using FlightAi.Core.Pricing;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace FlightAi.Agents;

/// <summary>
/// A language model may never author a number that reaches the user. This agent is handed only opaque
/// tokens — never a price, a duration, or a stop count — and writes prose that references them. Rendering the
/// tokens into real digits happens afterwards, in <see cref="ExplanationPlaceholderRenderer"/>, which
/// this agent has no access to and no knowledge of.
/// </summary>
public static class ExplanationAgentFactory
{
    private const string Instructions = """
        You explain a ranked list of flight offers to a traveller in plain, natural prose. Every offer
        is given to you only as opaque tokens, never as numbers. Reference {{PRICE_<id>}},
        {{DURATION_<id>}}, {{STOPS_<id>}}, {{REFUNDABLE_<id>}} and the comparison tokens
        {{PRICE_DELTA_<a>_vs_<b>}} / {{DURATION_DELTA_<a>_vs_<b>}} exactly as given, inside double
        curly braces. Never write a number, a currency amount, or a duration yourself — if you were
        not given a token for it, do not state it. Lead with the top-ranked offer and call out the
        sharpest trade-off against the next-best option.
        """;

    /// <summary>Builds the agent against a real model. Swap <paramref name="chatClient"/> for an Azure OpenAI / Foundry-backed client — nothing else here changes.</summary>
    public static AIAgent Create(IChatClient chatClient) =>
        chatClient.AsAIAgent(
            instructions: Instructions,
            name: "explanation-agent",
            description: "Explains ranked, priced offers in prose without ever authoring a number.");

    /// <summary>Same agent, wired to the fully offline mock — see <see cref="OfflineChatClient"/>.</summary>
    public static AIAgent CreateOffline() => Create(new OfflineChatClient(RespondWithExplanation));

    /// <summary>The prompt is the token vocabulary, JSON-serialized — never a raw <c>Offer</c>, and never a number.</summary>
    public static string BuildPrompt(IReadOnlyList<AgentVisibleOffer> offers, IReadOnlyList<string> comparisonTokensAvailable) =>
        $"Explain these ranked offers to the traveller:\n{JsonSerializer.Serialize(new PromptPayload(offers, comparisonTokensAvailable))}";

    private sealed record PromptPayload(
        IReadOnlyList<AgentVisibleOffer> Offers,
        IReadOnlyList<string> ComparisonTokensAvailable);

    private static string RespondWithExplanation(IReadOnlyList<ChatMessage> messages, ChatOptions? options)
    {
        var userMessage = messages.LastOrDefault(m => m.Role == ChatRole.User)?.Text ?? "{}";
        var jsonStart = userMessage.IndexOf('{');
        var payload = JsonSerializer.Deserialize<PromptPayload>(userMessage[jsonStart..])
                      ?? new PromptPayload([], []);

        var ranked = payload.Offers.OrderBy(o => o.Rank).ToList();
        if (ranked.Count == 0) return "No offers to explain.";

        var top = ranked[0];
        var sb = new StringBuilder();
        sb.Append(
            $"The best match is the {top.Carrier} option at {Tok(top.PriceToken)} — " +
            $"{Tok(top.StopsToken)}, {Tok(top.DurationToken)} total, {Tok(top.RefundableToken)}.");

        if (ranked.Count > 1)
        {
            var runnerUp = ranked[1];
            var priceDelta = Tok($"PRICE_DELTA_{runnerUp.OfferId}_vs_{top.OfferId}");
            var durationDelta = Tok($"DURATION_DELTA_{runnerUp.OfferId}_vs_{top.OfferId}");
            sb.Append(
                $" The next-best option, on {runnerUp.Carrier}, is {priceDelta} and " +
                $"{durationDelta} than the top pick — worth it only if that trade-off matches " +
                $"what the traveller actually asked for.");
        }

        return sb.ToString();
    }

    /// <summary>Wraps a token name as the literal <c>{{TOKEN}}</c> placeholder text — plain concatenation, deliberately not string interpolation, so there is no brace-escaping to get subtly wrong.</summary>
    private static string Tok(string name) => "{{" + name + "}}";
}

using FlightAi.Agents.Models.Explanation;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace FlightAi.Agents.Services.Explanation;

/// <summary>
/// Builds the explanation agent: ranked, tokenised offers in, prose out — written entirely from
/// opaque tokens (task 01), never a real value. See docs/05-agents-and-intent.md and
/// docs/specs/tasks/11-explanation-agent.md.
/// </summary>
public static class ExplanationAgentFactory
{
    /// <param name="language">The traveller's own query language (task 10's <c>SearchRequest.Language</c>),
    /// decided once by the intent agent — not re-detected or re-asked here.</param>
    public static ExplanationAgent Create(IChatClient chatClient, string language) =>
        new(chatClient.AsAIAgent(instructions: BuildInstructions(language)));

    private static string BuildInstructions(string language) =>
        $"Write a short, friendly explanation of the given flight offers for the traveller, entirely in " +
        $"{LanguageName(language)}. Reference every price, duration, stop count, and refund status only " +
        "using the exact {{TOKEN}} placeholders given to you — never write one as a digit or spell it out " +
        "in words. Every number the traveller needs already has a token; you never need to write a number " +
        "yourself.";

    private static string LanguageName(string language) => language switch
    {
        "pt-BR" => "Brazilian Portuguese",
        "en" => "English",
        _ => language,
    };
}

/// <summary>
/// Wraps the underlying <see cref="AIAgent"/>. Deliberately holds no reference to
/// <c>PriceReferenceStore</c> — it cannot resolve a token even if compromised, because it has no
/// member through which to do so. Rendering happens at the call site, after <see cref="ExplainAsync"/>
/// returns, never inside this class.
/// </summary>
public sealed class ExplanationAgent(AIAgent agent)
{
    public async Task<string> ExplainAsync(IReadOnlyList<TokenizedOffer> offers, CancellationToken cancellationToken = default)
    {
        var prompt = BuildPrompt(offers);
        var response = await agent.RunAsync(prompt, cancellationToken: cancellationToken);
        return response.Text;
    }

    private static string BuildPrompt(IReadOnlyList<TokenizedOffer> offers) =>
        string.Join(
            Environment.NewLine,
            offers.Select(offer =>
                $"Offer {offer.OfferId}: price {offer.PriceToken}, duration {offer.DurationToken}, " +
                $"{offer.StopsToken}, refund policy {offer.RefundableToken}."));
}

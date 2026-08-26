using System.Text.Json;
using FlightAi.Agents.Models.Intent;
using FlightAi.Core.Models.Offers;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace FlightAi.Agents.Services.Intent;

/// <summary>
/// Builds the intent-parsing agent: natural language in, a typed, schema-validated
/// <see cref="SearchRequest"/> out. See docs/05-agents-and-intent.md and
/// docs/specs/tasks/10-intent-agent.md.
/// </summary>
public static class IntentAgentFactory
{
    private const string Instructions =
        "Extract flight search parameters from the traveller's query as JSON matching the requested " +
        "schema. Infer the Language field from the language the query itself was written in — never " +
        "ask for it separately.";

    public static IntentAgent Create(IChatClient chatClient) => new(chatClient.AsAIAgent(instructions: Instructions));
}

/// <summary>
/// Wraps the underlying <see cref="AIAgent"/>. Nothing downstream of <see cref="ParseAsync"/> ever
/// reads free text again — every later pipeline stage works with the typed <see cref="SearchRequest"/>.
/// </summary>
public sealed class IntentAgent(AIAgent agent)
{
    public async Task<IntentResult> ParseAsync(string query, CancellationToken cancellationToken = default)
    {
        SearchRequest parsed;
        try
        {
            var response = await agent.RunAsync<SearchRequest>(query, cancellationToken: cancellationToken);
            parsed = response.Result;
        }
        catch (JsonException ex)
        {
            // Microsoft.Agents.AI's RunAsync<T> throws on unparseable model output rather than
            // returning a failure value (verified empirically, not assumed) -- this is the one place
            // that exception is caught and translated into this project's "return, don't throw"
            // convention, matching task 04's for suppliers.
            return IntentResult.Failed($"model output was not valid JSON: {ex.Message}");
        }

        // Validation happens here, after the typed parse, in deterministic code -- never delegated to
        // the model's own judgement. RunAsync<T> only guarantees the *shape* is right (valid JSON
        // matching SearchRequest's properties); it says nothing about whether the values make sense.
        return Validate(parsed);
    }

    private static IntentResult Validate(SearchRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Origin))
            return IntentResult.Failed("missing origin");
        if (string.IsNullOrWhiteSpace(request.Destination))
            return IntentResult.Failed("missing destination");
        if (request.PassengerCount < 1)
            return IntentResult.Failed("passenger count must be at least 1");
        if (string.IsNullOrWhiteSpace(request.Language))
            return IntentResult.Failed("missing language");
        if (request.DepartureDate < DateOnly.FromDateTime(DateTime.UtcNow))
            return IntentResult.Failed("departure date is in the past");

        return IntentResult.Ok(request);
    }
}

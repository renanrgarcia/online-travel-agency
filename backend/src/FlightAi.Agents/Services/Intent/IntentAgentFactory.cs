using System.Text.Json;
using FlightAi.Agents.Models.Intent;
using FlightAi.Core.Models.Offers;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace FlightAi.Agents.Services.Intent;

/// <summary>
/// Builds the intent-parsing agent: natural language in, a typed, schema-validated
/// <see cref="SearchRequest"/> out. See docs/reference/05-agents-and-intent.md and
/// docs/features/01-backend/tasks/10-intent-agent.md.
/// </summary>
public static class IntentAgentFactory
{
    // Found live against a real model (task 17): without an exact format specified, the model returned
    // "pt" for a Portuguese query instead of "pt-BR" -- PriceReferenceStore's localization is a strict
    // "pt-BR" equality check (task 18), so every resolved token silently fell back to English inside
    // otherwise-Portuguese prose. OfflineChatClient never exercised this since its canned responses
    // hardcode the exact string.
    //
    // Origin/Destination as IATA codes (task 25): the mock connectors never read these fields, so the
    // gap was invisible until DuffelConnector needed real, resolvable airport codes to call a real API
    // with. Every existing fixture already used codes like "GRU"/"LIS" -- this makes that assumption an
    // explicit instruction instead of one nothing enforced.
    private const string Instructions =
        "Extract flight search parameters from the traveller's query as JSON matching the requested " +
        "schema. Infer the Language field from the language the query itself was written in — never " +
        "ask for it separately. Language must be exactly \"en\" for English or \"pt-BR\" for Portuguese " +
        "— never a bare language code like \"pt\" or a different regional variant. " +
        "DepartureDate must be an ISO 8601 date string in exactly yyyy-MM-dd format. " +
        "If the traveller does not provide a departure date, return null; never invent or infer a date. " +
        "Origin and Destination must each be a 3-letter IATA airport code (e.g. \"GRU\", \"LIS\") — " +
        "translate a city or airport name to its IATA code yourself; never return a free-text place name.";

    public static IntentAgent Create(IChatClient chatClient)
    {
        var capturingClient = new CapturingChatClient(chatClient);
        return new(capturingClient.AsAIAgent(instructions: Instructions), capturingClient);
    }
}

/// <summary>
/// Wraps the underlying <see cref="AIAgent"/>. Nothing downstream of <see cref="ParseAsync"/> ever
/// reads free text again — every later pipeline stage works with the typed <see cref="SearchRequest"/>.
/// </summary>
public sealed class IntentAgent(AIAgent agent, CapturingChatClient capturingClient)
{
    public async Task<IntentResult> ParseAsync(string query, CancellationToken cancellationToken = default)
    {
        capturingClient.LastResponseText = null;
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
            return IntentResult.Failed(
                $"model output was not valid JSON: {ex.Message}",
                capturingClient.LastResponseText);
        }

        // Validation happens here, after the typed parse, in deterministic code -- never delegated to
        // the model's own judgement. RunAsync<T> only guarantees the *shape* is right (valid JSON
        // matching SearchRequest's properties); it says nothing about whether the values make sense.
        return Validate(parsed, capturingClient.LastResponseText);
    }

    private static IntentResult Validate(SearchRequest request, string? rawModelResponse)
    {
        if (string.IsNullOrWhiteSpace(request.Origin))
            return IntentResult.Failed("missing origin", rawModelResponse);
        if (string.IsNullOrWhiteSpace(request.Destination))
            return IntentResult.Failed("missing destination", rawModelResponse);
        if (request.PassengerCount < 1)
            return IntentResult.Failed("passenger count must be at least 1", rawModelResponse);
        if (string.IsNullOrWhiteSpace(request.Language))
            return IntentResult.Failed("missing language", rawModelResponse);
        if (request.DepartureDate is null)
            return IntentResult.Failed("missing departure date", rawModelResponse, code: "missing-departure-date");
        if (request.DepartureDate < DateOnly.FromDateTime(DateTime.UtcNow))
            return IntentResult.Failed("departure date is in the past", rawModelResponse);

        return IntentResult.Ok(request);
    }
}

public sealed class CapturingChatClient(IChatClient inner) : IChatClient
{
    public string? LastResponseText { get; set; }

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var response = await inner.GetResponseAsync(messages, options, cancellationToken);
        LastResponseText = response.Text;
        return response;
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var update in inner.GetStreamingResponseAsync(messages, options, cancellationToken))
        {
            LastResponseText = update.Text;
            yield return update;
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => inner.GetService(serviceType, serviceKey);

    public void Dispose() => inner.Dispose();
}

using System.Text.Json;
using System.Text.RegularExpressions;
using FlightAi.Core.Offers;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace FlightAi.Agents;

/// <summary>
/// Intent parsing: natural language in, a typed <see cref="SearchRequest"/> out,
/// validated against a schema before it ever reaches a supplier. Microsoft Agent Framework's
/// <c>RunAsync&lt;T&gt;</c> is what does the "validated against a schema" part — this class just
/// wires an agent up to call it.
/// </summary>
public static class IntentAgentFactory
{
    private const string Instructions = """
        You turn a traveller's natural-language flight request into a structured search request.
        Extract origin and destination as IATA airport codes, the departure date, an optional return
        date, traveller counts, cabin class, and any stated preferences (avoiding red-eyes, seat
        preference, a maximum number of stops). If a detail is not stated, use a sensible default
        rather than asking a follow-up question — this agent's whole job is to produce one structured
        object, not to hold a conversation.
        """;

    /// <summary>Builds the agent against a real model. Swap <paramref name="chatClient"/> for an Azure OpenAI / Foundry-backed client — nothing else here changes.</summary>
    public static AIAgent Create(IChatClient chatClient) =>
        chatClient.AsAIAgent(
            instructions: Instructions,
            name: "intent-agent",
            description: "Parses a natural-language flight request into a typed SearchRequest.");

    /// <summary>
    /// Builds the same agent against a fully offline mock so the sample runs with no API key. The
    /// keyword parsing below is deliberately weak — it exists to prove the wiring (structured,
    /// schema-validated output), not to demonstrate real natural-language understanding. Resolving
    /// something like "first week of December" properly is exactly the fuzzy work you want a real
    /// model doing instead of this.
    /// </summary>
    public static AIAgent CreateOffline() => Create(new OfflineChatClient(RespondWithParsedIntent));

    private static readonly Dictionary<string, string> Airports = new()
    {
        ["lisbon"] = "LIS",
        ["são paulo"] = "GRU",
        ["sao paulo"] = "GRU",
        ["new york"] = "JFK",
        ["london"] = "LHR",
        ["madrid"] = "MAD",
        ["istanbul"] = "IST"
    };

    private static string RespondWithParsedIntent(IReadOnlyList<ChatMessage> messages, ChatOptions? options)
    {
        var utterance = messages.LastOrDefault(m => m.Role == ChatRole.User)?.Text ?? "";
        var lower = utterance.ToLowerInvariant();

        var origin = Airports.Keys.Where(lower.Contains).Select(c => Airports[c]).FirstOrDefault() ?? "GRU";
        var destination = Airports.Keys
            .Where(city => lower.Contains(city) && Airports[city] != origin)
            .Select(city => Airports[city])
            .FirstOrDefault() ?? "LIS";

        var request = new SearchRequest
        {
            Origin = origin,
            Destination = destination,
            DepartureDate = ExtractDepartureDate(lower),
            Travellers = new TravellerCounts(Adults: ExtractAdults(lower)),
            Cabin = lower.Contains("business") ? CabinClass.Business : CabinClass.Economy,
            Preferences = new SearchPreferences(
                AvoidRedEyes: lower.Contains("no red-eye") || lower.Contains("no red eye"),
                SeatPreference: lower.Contains("aisle") ? "aisle" : lower.Contains("window") ? "window" : null,
                MaxStops: lower.Contains("nonstop") || lower.Contains("direct") ? 0 : null)
        };

        return JsonSerializer.Serialize(request);
    }

    private static int ExtractAdults(string lower)
    {
        var match = Regex.Match(lower, @"(\d+)\s+adults?");
        return match.Success ? int.Parse(match.Groups[1].Value) : 1;
    }

    private static readonly string[] MonthNames =
    [
        "january", "february", "march", "april", "may", "june",
        "july", "august", "september", "october", "november", "december"
    ];

    private static DateOnly ExtractDepartureDate(string lower)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        for (var i = 0; i < MonthNames.Length; i++)
        {
            if (!lower.Contains(MonthNames[i])) continue;
            var month = i + 1;
            var year = today.Month <= month ? today.Year : today.Year + 1;
            return new DateOnly(year, month, 1);
        }
        return today.AddMonths(1);
    }
}

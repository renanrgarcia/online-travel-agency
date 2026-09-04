using System.Reflection;
using FlightAi.Agents.Services;
using FlightAi.Agents.Services.Intent;
using FlightAi.Core.Models.Offers;
using Xunit;

namespace FlightAi.Tests;

/// <summary>
/// One test per eval in docs/features/01-backend/tasks/10-intent-agent.md, against an <see cref="OfflineChatClient"/>
/// registered with canned JSON responses. RunAsync&lt;T&gt;'s actual deserialization behavior (a
/// missing field leaves the string null rather than throwing; invalid JSON throws
/// System.Text.Json.JsonException) was verified empirically before writing these tests, not assumed.
/// </summary>
public class IntentAgentFactoryTests
{
    private const string EnglishQuery = "cheapest flight from São Paulo to Lisbon on 12 March for 2 people";
    private const string PortugueseQuery = "voo mais barato de São Paulo para Lisboa em 12 de março para 2 pessoas";
    private const string MissingDestinationQuery = "cheapest flight from GRU somewhere, not sure where";
    private const string MalformedQuery = "this will make the model reply with garbage";
    private const string PastDateQuery = "cheapest flight from GRU to LIS back in January 2020";

    private static IntentAgent NewAgent()
    {
        var client = new OfflineChatClient()
            .RegisterResponse(EnglishQuery,
                """{"Origin":"GRU","Destination":"LIS","DepartureDate":"2027-03-12","PassengerCount":2,"Language":"en"}""")
            .RegisterResponse(PortugueseQuery,
                """{"Origin":"GRU","Destination":"LIS","DepartureDate":"2027-03-12","PassengerCount":2,"Language":"pt-BR"}""")
            .RegisterResponse(MissingDestinationQuery,
                """{"Origin":"GRU","DepartureDate":"2027-03-12","PassengerCount":2,"Language":"en"}""")
            .RegisterResponse(MalformedQuery, "sorry, I cannot help with that")
            .RegisterResponse(PastDateQuery,
                """{"Origin":"GRU","Destination":"LIS","DepartureDate":"2020-01-01","PassengerCount":2,"Language":"en"}""");

        return IntentAgentFactory.Create(client);
    }

    [Fact] // E1 — baseline extraction
    public async Task E1_EnglishQuery_ProducesACorrectlyPopulatedSearchRequest()
    {
        var result = await NewAgent().ParseAsync(EnglishQuery);

        Assert.True(result.Success);
        Assert.Equal("GRU", result.Request!.Origin);
        Assert.Equal("LIS", result.Request.Destination);
        Assert.Equal(new DateOnly(2027, 3, 12), result.Request.DepartureDate);
        Assert.Equal(2, result.Request.PassengerCount);
    }

    [Fact] // E2 — determinism through the agent layer
    public async Task E2_SameInputTwice_ProducesAnIdenticalSearchRequest()
    {
        var agent = NewAgent();

        var first = await agent.ParseAsync(EnglishQuery);
        var second = await agent.ParseAsync(EnglishQuery);

        Assert.Equal(first, second);
    }

    [Fact] // E3 — a half-filled request flowing downstream would produce a confidently wrong search
    public async Task E3_MissingDestination_IsRejectedRatherThanGuessed()
    {
        var result = await NewAgent().ParseAsync(MissingDestinationQuery);

        Assert.False(result.Success);
        Assert.Null(result.Request);
        Assert.Contains("destination", result.FailureReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact] // E4 — real models return junk sometimes; this is expected, not exceptional
    public async Task E4_UnparseableModelOutput_SurfacesAsAFailureNotAnException()
    {
        var result = await NewAgent().ParseAsync(MalformedQuery);

        Assert.False(result.Success);
        Assert.NotNull(result.FailureReason);
        Assert.Equal("sorry, I cannot help with that", result.RawModelResponse);
    }

    [Fact] // E5 — schema validation includes semantic validity, not just shape
    public async Task E5_PastDepartureDate_IsRejectedByValidation()
    {
        var result = await NewAgent().ParseAsync(PastDateQuery);

        Assert.False(result.Success);
        Assert.Contains("past", result.FailureReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact] // E6 — the boundary claim from docs/reference/01-architecture-overview.md, made testable
    public async Task E6_SuccessfulResult_IsTheTypedSearchRequestWithNoFreeTextFieldAnywhere()
    {
        var result = await NewAgent().ParseAsync(EnglishQuery);

        Assert.IsType<SearchRequest>(result.Request);

        // SearchRequest carries exactly these five typed fields -- no raw-text/query field exists for
        // the original free-form input to ride along in.
        var propertyNames = typeof(SearchRequest).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .ToHashSet();
        Assert.Equal(
            new HashSet<string> { "Origin", "Destination", "DepartureDate", "PassengerCount", "Language" },
            propertyNames);
    }

    [Fact] // E7 — the target market is Brazilian; monolingual intent parsing would be a product bug
    public async Task E7_PortugueseQuery_ParsesEquivalentlyToTheEnglishOne()
    {
        var result = await NewAgent().ParseAsync(PortugueseQuery);

        Assert.True(result.Success);
        Assert.Equal("GRU", result.Request!.Origin);
        Assert.Equal("LIS", result.Request.Destination);
        Assert.Equal(new DateOnly(2027, 3, 12), result.Request.DepartureDate);
        Assert.Equal(2, result.Request.PassengerCount);
    }

    [Fact] // E8 — Language has to actually be populated correctly, not just present as an unused field
    public async Task E8_Language_IsPopulatedPerQueryLanguageNotHardcoded()
    {
        var agent = NewAgent();

        var english = await agent.ParseAsync(EnglishQuery);
        var portuguese = await agent.ParseAsync(PortugueseQuery);

        Assert.Equal("en", english.Request!.Language);
        Assert.Equal("pt-BR", portuguese.Request!.Language);
    }
}

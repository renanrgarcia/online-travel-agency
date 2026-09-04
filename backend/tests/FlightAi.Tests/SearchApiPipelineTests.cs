using System.Net;
using System.Text.Json;
using FlightAi.Agents.Services;
using FlightAi.Agents.Services.Intent;
using FlightAi.Api;
using FlightAi.Core.Interfaces.Suppliers;
using FlightAi.Core.Models.Suppliers;
using FlightAi.Core.Services.Pricing;
using FlightAi.Core.Services.Suppliers;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FlightAi.Tests;

/// <summary>
/// One test per eval in docs/features/01-backend/tasks/13-search-api-sse-full-pipeline.md, against a real HTTP
/// request through <see cref="WebApplicationFactory{TEntryPoint}"/> with a custom
/// <see cref="IChatClient"/> (and, for E2, custom connectors) registered per test -- the same
/// deterministic mock connectors (task 05) and default scoring weights (task 03) rank LCC-002 first in
/// every test that doesn't deliberately fail it, per DemoOfflineChatClient's own comment.
/// </summary>
public class SearchApiPipelineTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private const string Query = "cheapest flight from São Paulo to Lisbon";
    private const string NormalIntentJson =
        """{"Origin":"GRU","Destination":"LIS","DepartureDate":"2027-03-12","PassengerCount":2,"Language":"en"}""";

    // Test-only, never a real secret -- Program.cs requires PriceAssertion:SigningKey to be configured
    // (task 21) and there's no safe default, so every test hitting the real HTTP pipeline needs one
    // supplied via UseSetting, the same mechanism a deployed environment would use for the real key.
    private const string TestSigningKey = "test-signing-key-not-a-real-secret";

    private static SupplierFanOutOrchestrator DefaultOrchestrator(params ISupplierConnector[] connectors)
    {
        var policy = new SupplierPolicy(TimeSpan.FromSeconds(5), 100, TimeSpan.FromMinutes(1), 3, TimeSpan.FromMinutes(1));
        return new SupplierFanOutOrchestrator(connectors, connectors.ToDictionary(c => c.Name, _ => policy));
    }

    private WebApplicationFactory<Program> WithServices(IChatClient chatClient, SupplierFanOutOrchestrator? orchestrator = null) =>
        factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("PriceAssertion:SigningKey", TestSigningKey);
            builder.ConfigureServices(services =>
            {
                // Last registration wins for a single (non-IEnumerable) service resolution -- the
                // standard ASP.NET Core pattern for overriding a service in WebApplicationFactory tests.
                services.AddSingleton(chatClient);
                if (orchestrator is not null)
                    services.AddSingleton(orchestrator);
            });
        });

    private static async Task<List<(string EventType, string Data)>> ReadAllEventsAsync(HttpResponseMessage response)
    {
        await using var stream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream);
        var events = new List<(string, string)>();
        string? eventType = null;

        string? line;
        while ((line = await reader.ReadLineAsync()) is not null)
        {
            if (line.StartsWith("event:", StringComparison.Ordinal))
                eventType = line["event:".Length..].Trim();
            else if (line.StartsWith("data:", StringComparison.Ordinal) && eventType is not null)
                events.Add((eventType, line["data:".Length..].Trim()));
        }

        return events;
    }

    [Fact] // E1 — the contract
    public async Task E1_NormalSearch_EmitsAllFourEventTypesInOrder()
    {
        var client = new OfflineChatClient()
            .RegisterResponse("São Paulo", NormalIntentJson)
            .RegisterResponse("Offer LCC-002", "Best pick: {{PRICE_LCC-002}}.");
        using var http = WithServices(client).CreateClient();

        var response = await http.GetAsync($"/api/search/stream?q={Uri.EscapeDataString(Query)}");
        var events = await ReadAllEventsAsync(response);

        var order = events.Select(e => e.EventType).Distinct().ToList();
        Assert.Equal(["parsed-intent", "supplier-result", "ranked-offers", "explanation"], order);
    }

    [Fact]
    public async Task ModelProviderFailure_IsReportedAsAiUnavailableSseError()
    {
        using var http = WithServices(new OfflineChatClient()).CreateClient();

        var response = await http.GetAsync($"/api/search/stream?q={Uri.EscapeDataString(Query)}");
        var events = await ReadAllEventsAsync(response);
        var error = Assert.Single(events);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("error", error.EventType);
        using var payload = JsonDocument.Parse(error.Data);
        Assert.Equal("ai-unavailable", payload.RootElement.GetProperty("code").GetString());
    }

    [Fact] // E2 — per-stage streaming is real, the reason for SSE at all
    public async Task E2_ConnectorsWithDifferentDelays_SupplierResultsArriveIncrementally()
    {
        var client = new OfflineChatClient()
            .RegisterResponse("São Paulo", NormalIntentJson)
            .RegisterResponse("Offer LCC-002", "Best pick: {{PRICE_LCC-002}}.");
        var orchestrator = DefaultOrchestrator(
            new MockGdsConnector(TimeSpan.FromMilliseconds(200)),
            new MockNdcConnector(TimeSpan.FromMilliseconds(800)),
            new MockLccConnector());
        using var http = WithServices(client, orchestrator).CreateClient();

        var start = DateTimeOffset.UtcNow;
        using var response = await http.GetAsync(
            $"/api/search/stream?q={Uri.EscapeDataString(Query)}", HttpCompletionOption.ResponseHeadersRead);
        await using var stream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream);

        var supplierResultTimestamps = new List<TimeSpan>();
        string? eventType = null;
        string? line;
        while ((line = await reader.ReadLineAsync()) is not null)
        {
            if (line.StartsWith("event:", StringComparison.Ordinal))
                eventType = line["event:".Length..].Trim();
            else if (line.StartsWith("data:", StringComparison.Ordinal) && eventType == "supplier-result")
                supplierResultTimestamps.Add(DateTimeOffset.UtcNow - start);
        }

        Assert.Equal(3, supplierResultTimestamps.Count);
        // LCC (no delay) first, GDS (~200ms) second, NDC (~800ms) last -- true completion order.
        Assert.True(supplierResultTimestamps[0] < TimeSpan.FromMilliseconds(150));
        Assert.True(supplierResultTimestamps[1] > TimeSpan.FromMilliseconds(100) && supplierResultTimestamps[1] < TimeSpan.FromMilliseconds(500));
        Assert.True(supplierResultTimestamps[2] > TimeSpan.FromMilliseconds(600));
    }

    [Fact] // E3 — nothing half-rendered reaches a browser
    public async Task E3_ExplanationPayload_ContainsResolvedPricesAndNoTokens()
    {
        var client = new OfflineChatClient()
            .RegisterResponse("São Paulo", NormalIntentJson)
            .RegisterResponse("Offer LCC-002", "Best pick: {{PRICE_LCC-002}}, taking {{DURATION_LCC-002}}.");
        using var http = WithServices(client).CreateClient();

        var events = await ReadAllEventsAsync(await http.GetAsync($"/api/search/stream?q={Uri.EscapeDataString(Query)}"));
        var payload = JsonSerializer.Deserialize<JsonElement>(events.Single(e => e.EventType == "explanation").Data);

        // "raw" legitimately still contains "{{" -- it's the model's literal pre-render output, kept
        // for a debug view (docs/reference/06-api-sse-contract.md). Only "text" -- what a browser shows -- must
        // be clean of both.
        Assert.Contains("$590.00", payload.GetProperty("text").GetString());
        Assert.DoesNotContain("{{", payload.GetProperty("text").GetString());
    }

    [Fact] // E4 — end-to-end price integrity over HTTP
    public async Task E4_ExplanationPrice_MatchesTheRegisteredValueExactly()
    {
        var client = new OfflineChatClient()
            .RegisterResponse("São Paulo", NormalIntentJson)
            .RegisterResponse("Offer LCC-002", "The price is {{PRICE_LCC-002}} exactly.");
        using var http = WithServices(client).CreateClient();

        var events = await ReadAllEventsAsync(await http.GetAsync($"/api/search/stream?q={Uri.EscapeDataString(Query)}"));
        var payload = JsonSerializer.Deserialize<JsonElement>(events.Single(e => e.EventType == "explanation").Data);

        Assert.Equal("The price is $590.00 exactly.", payload.GetProperty("text").GetString());
    }

    [Fact] // E5 — degradation survives the transport layer
    public async Task E5_OneConnectorFailing_StillCompletesWithRankedOffersAndExplanation()
    {
        var client = new OfflineChatClient()
            .RegisterResponse(
                "fails on purpose",
                """{"Origin":"GRU","Destination":"LIS-FAIL-SEARCH-NDC","DepartureDate":"2027-03-12","PassengerCount":2,"Language":"en"}""")
            .RegisterResponse("Offer LCC-002", "Best pick: {{PRICE_LCC-002}}.");
        using var http = WithServices(client).CreateClient();

        var events = await ReadAllEventsAsync(await http.GetAsync("/api/search/stream?q=fails+on+purpose"));

        var ndcResult = events.First(e => e.EventType == "supplier-result" && e.Data.Contains("NDC", StringComparison.Ordinal));
        Assert.Contains("Failed", ndcResult.Data);
        Assert.Contains("ranked-offers", events.Select(e => e.EventType));
        Assert.Contains("explanation", events.Select(e => e.EventType));
    }

    [Fact] // E6 — the guard protects the client, not just the test suite
    public async Task E6_ModelEmittingARawDigit_ProducesAnUncleanExplanationNotAMalformedOne()
    {
        var client = new OfflineChatClient()
            .RegisterResponse("São Paulo", NormalIntentJson)
            .RegisterResponse("Offer LCC-002", "Best pick: {{PRICE_LCC-002}}, only $999 today!");
        using var http = WithServices(client).CreateClient();

        var events = await ReadAllEventsAsync(await http.GetAsync($"/api/search/stream?q={Uri.EscapeDataString(Query)}"));
        var payload = JsonSerializer.Deserialize<JsonElement>(events.Single(e => e.EventType == "explanation").Data);

        Assert.False(payload.GetProperty("isClean").GetBoolean());
        Assert.Equal("", payload.GetProperty("text").GetString());
        Assert.Contains("999", payload.GetProperty("raw").GetString());
    }

    [Fact] // E7 — a missing or duplicated entry is client-visible
    public async Task E7_EveryConnector_AppearsExactlyOnceInSupplierResults()
    {
        var client = new OfflineChatClient()
            .RegisterResponse("São Paulo", NormalIntentJson)
            .RegisterResponse("Offer LCC-002", "Best pick: {{PRICE_LCC-002}}.");
        using var http = WithServices(client).CreateClient();

        var events = await ReadAllEventsAsync(await http.GetAsync($"/api/search/stream?q={Uri.EscapeDataString(Query)}"));
        var supplierResults = events.Where(e => e.EventType == "supplier-result").Select(e => e.Data).ToList();

        Assert.Equal(3, supplierResults.Count);
        Assert.Contains(supplierResults, d => d.Contains("GDS"));
        Assert.Contains(supplierResults, d => d.Contains("NDC"));
        Assert.Contains(supplierResults, d => d.Contains("LCC"));
    }

    [Fact] // E8 — task 12 E5, with real cost attached
    public async Task E8_CancelledAfterParsedIntent_StopsWithoutWaitingOutTheSupplierTimeout()
    {
        // Connectors that take 5s each: if cancellation didn't actually stop work, this run would take
        // ~5s regardless of the 100ms cancellation. Tasks 04-07 already established that a cancelled
        // connector reports SupplierStatus.Cancelled rather than throwing (graceful degradation, not
        // an exception) -- so what proves "cancellation stops wasted work" here isn't an exception
        // type, it's that the whole pipeline finishes fast instead of waiting out the 5s delay.
        var chatClient = new OfflineChatClient().RegisterResponse("São Paulo", NormalIntentJson);
        var intentAgent = IntentAgentFactory.Create(chatClient);
        var orchestrator = DefaultOrchestrator(
            new MockGdsConnector(TimeSpan.FromSeconds(5)), new MockNdcConnector(TimeSpan.FromSeconds(5)), new MockLccConnector(TimeSpan.FromSeconds(5)));
        var priceAssertionService = new PriceAssertionService(TestSigningKey, TimeSpan.FromMinutes(5));
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100)); // parsed-intent returns near-instantly; this lands just after it

        var eventTypes = new List<string>();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        await foreach (var item in SearchPipeline.RunAsync(Query, intentAgent, orchestrator, chatClient, priceAssertionService, cts.Token))
            eventTypes.Add(item.EventType!);
        stopwatch.Stop();

        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(2),
            $"expected cancellation to cut the 5s connector delay short, took {stopwatch.Elapsed}");
        Assert.Contains("parsed-intent", eventTypes);
        // Every connector reports Cancelled (a status, not a thrown exception, per task 06's own
        // design) -- ranking and explanation still run, on zero offers, and finish fast rather than
        // hang, which is the actual cost this eval cares about.
        Assert.Contains("explanation", eventTypes);
    }

    [Fact] // E9 — determinism end to end, offline model
    public async Task E9_TwoIdenticalSearches_ProduceIdenticalEventSequences()
    {
        var client = new OfflineChatClient()
            .RegisterResponse("São Paulo", NormalIntentJson)
            .RegisterResponse("Offer LCC-002", "Best pick: {{PRICE_LCC-002}}.");
        using var http = WithServices(client).CreateClient();

        var first = await ReadAllEventsAsync(await http.GetAsync($"/api/search/stream?q={Uri.EscapeDataString(Query)}"));
        var second = await ReadAllEventsAsync(await http.GetAsync($"/api/search/stream?q={Uri.EscapeDataString(Query)}"));

        // parsed-intent, supplier-result, and explanation are still byte-identical across runs -- the
        // original guarantee this eval predates task 21 by. ranked-offers now carries a price
        // assertion per offer, whose expiry and signature are deliberately fresh on every issue (task
        // 21 E8), so it's compared field by field instead: the business data stays deterministic, only
        // the assertion legitimately differs.
        Assert.Equal(
            first.Where(e => e.EventType != "ranked-offers"),
            second.Where(e => e.EventType != "ranked-offers"));

        var firstOffers = JsonSerializer.Deserialize<JsonElement>(first.Single(e => e.EventType == "ranked-offers").Data);
        var secondOffers = JsonSerializer.Deserialize<JsonElement>(second.Single(e => e.EventType == "ranked-offers").Data);
        Assert.Equal(firstOffers.GetArrayLength(), secondOffers.GetArrayLength());
        for (var i = 0; i < firstOffers.GetArrayLength(); i++)
        {
            var (a, b) = (firstOffers[i], secondOffers[i]);
            Assert.Equal(a.GetProperty("offerId").GetString(), b.GetProperty("offerId").GetString());
            Assert.Equal(a.GetProperty("price").GetDecimal(), b.GetProperty("price").GetDecimal());
            Assert.Equal(a.GetProperty("score").GetDecimal(), b.GetProperty("score").GetDecimal());
            // Deliberately different (task 21 E8) -- confirms freshness holds, not just business data.
            Assert.NotEqual(
                a.GetProperty("priceAssertion").GetProperty("signature").GetString(),
                b.GetProperty("priceAssertion").GetProperty("signature").GetString());
        }
    }
}

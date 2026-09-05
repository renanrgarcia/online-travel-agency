using System.Net;
using FlightAi.Agents.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FlightAi.Tests;

/// <summary>
/// One test per eval in docs/features/01-backend/tasks/23-error-handling-and-diagnostics.md.
/// <see cref="CapturingLoggerProvider"/> is a hand-rolled test double (this project's convention --
/// see <see cref="OfflineChatClient"/> -- no mocking library) that proves an unhandled exception reaches
/// <c>ILogger</c>, since that's what actually gets written to whichever destination is configured
/// (App Service filesystem logging, declared in infra/modules/app-service.bicep) -- the destination
/// itself is an infra concern, not something an in-memory test can observe.
/// </summary>
public class ErrorHandlingTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private const string Query = "cheapest flight from São Paulo to Lisbon";
    private const string NormalIntentJson =
        """{"Origin":"GRU","Destination":"LIS","DepartureDate":"2027-03-12","PassengerCount":2,"Language":"en"}""";

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        public List<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = [];

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(this);

        public void Dispose() { }

        private sealed class CapturingLogger(CapturingLoggerProvider owner) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter) =>
                owner.Entries.Add((logLevel, formatter(state, exception), exception));
        }
    }

    private (WebApplicationFactory<Program> Factory, CapturingLoggerProvider Logs) WithChatClient(OfflineChatClient chatClient)
    {
        var logs = new CapturingLoggerProvider();
        var configured = factory.WithWebHostBuilder(builder =>
        {
            // Program.cs now requires PriceAssertion:SigningKey to be configured (task 21) with no safe
            // default, so every test hitting the real HTTP pipeline needs one -- same key,
            // same mechanism SearchApiPipelineTests uses.
            builder.UseSetting("PriceAssertion:SigningKey", "test-signing-key-not-a-real-secret");
            // Forced empty regardless of the local machine's real user secrets (task 25) -- this class
            // never overrides SupplierFanOutOrchestrator, so it relies entirely on Program.cs's own
            // wiring, which would otherwise add a real, network-calling DuffelConnector whenever a
            // developer happens to have Duffel:ApiKey configured locally for manual testing.
            builder.UseSetting("Duffel:ApiKey", "");
            builder.ConfigureServices(services =>
            {
                // Last registration wins for a single (non-IEnumerable) service resolution, same pattern
                // SearchApiPipelineTests uses to override the chat client per test.
                services.AddSingleton<Microsoft.Extensions.AI.IChatClient>(chatClient);
                services.AddLogging(b => b.AddProvider(logs));
            });
        });
        return (configured, logs);
    }

    private static async Task<List<(string EventType, string Data)>> ReadEventsAsync(HttpResponseMessage response)
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

    [Fact] // E1 -- a model failure before the first event is reported as a structured SSE error
    public async Task E1_ModelFailureBeforeFirstEvent_ReturnsAiUnavailableSseError()
    {
        // No "São Paulo" rule registered -- OfflineChatClient throws exactly like the live incident did.
        var (configured, _) = WithChatClient(new OfflineChatClient());
        using var http = configured.CreateClient();

        var response = await http.GetAsync($"/api/search/stream?q={Uri.EscapeDataString(Query)}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);
        var events = await ReadEventsAsync(response);
        var error = Assert.Single(events);
        Assert.Equal("error", error.EventType);
        Assert.Contains("ai-unavailable", error.Data, StringComparison.Ordinal);
    }

    [Fact] // E2 -- provider failures are handled at the SSE boundary, not logged as unhandled exceptions
    public async Task E2_ModelFailure_IsNotAnUnhandledHttpException()
    {
        var (configured, logs) = WithChatClient(new OfflineChatClient());
        using var http = configured.CreateClient();

        var response = await http.GetAsync($"/api/search/stream?q={Uri.EscapeDataString(Query)}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain(logs.Entries, e => e.Level == LogLevel.Error && e.Exception is InvalidOperationException);
    }

    [Fact] // E3 -- exception-handling middleware must not touch the happy path
    public async Task E3_NormalSearch_StillCompletesAllFourEvents()
    {
        var client = new OfflineChatClient()
            .RegisterResponse("São Paulo", NormalIntentJson)
            .RegisterResponse("Offer LCC-002", "Best pick: {{PRICE_LCC-002}}.");
        var (configured, _) = WithChatClient(client);
        using var http = configured.CreateClient();

        var response = await http.GetAsync($"/api/search/stream?q={Uri.EscapeDataString(Query)}");
        var events = await ReadEventsAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            ["parsed-intent", "supplier-result", "ranked-offers", "explanation"],
            events.Select(e => e.EventType).Distinct());
    }

    [Fact] // E4 -- an exception after the response has already committed to text/event-stream
    public async Task E4_ExceptionMidStream_ConnectionEndsCleanlyRatherThanRewritingAlreadyCommittedHeaders()
    {
        // Intent parsing succeeds and ranked-offers streams, but no explanation response is registered,
        // so ExplainAsync throws once headers (200, text/event-stream) are already committed to the
        // client -- the other half of the SSE failure shape task 23 documents.
        var client = new OfflineChatClient().RegisterResponse("São Paulo", NormalIntentJson);
        var (configured, logs) = WithChatClient(client);
        using var http = configured.CreateClient();

        var response = await http.GetAsync(
            $"/api/search/stream?q={Uri.EscapeDataString(Query)}", HttpCompletionOption.ResponseHeadersRead);

        // The middleware can no longer rewrite the status or content type at this point -- proving it
        // doesn't try to is exactly what this eval is for.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);

        // Accumulate line-by-line into this outer list, not via the shared ReadEventsAsync helper --
        // if the read throws partway through (the expected outcome here), a helper's own local
        // accumulator would be lost along with the exception, discarding exactly the partial data
        // this eval needs to assert on.
        var events = new List<(string EventType, string Data)>();
        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync();
            using var reader = new StreamReader(stream);
            string? eventType = null;
            string? line;
            while ((line = await reader.ReadLineAsync()) is not null)
            {
                if (line.StartsWith("event:", StringComparison.Ordinal))
                    eventType = line["event:".Length..].Trim();
                else if (line.StartsWith("data:", StringComparison.Ordinal) && eventType is not null)
                    events.Add((eventType, line["data:".Length..].Trim()));
            }
        }
        catch (Exception ex) when (ex is IOException or HttpRequestException)
        {
            // Acceptable: a connection torn down mid-body can surface here as a truncated-read error
            // instead of a clean end-of-stream, depending on the transport. Either way, nothing
            // malformed reaches the assertions below -- only whatever was already read stays.
        }

        Assert.Contains(events, e => e.EventType == "ranked-offers");
        Assert.DoesNotContain(events, e => e.EventType == "explanation");
        Assert.Contains(logs.Entries, e => e.Level == LogLevel.Error && e.Exception is InvalidOperationException);
    }

    [Fact] // E5 -- diagnosable server-side, safe client-facing
    public async Task E5_ProblemDetailsResponse_DoesNotLeakTheExceptionMessageOrStackTrace()
    {
        var (configured, _) = WithChatClient(new OfflineChatClient());
        using var http = configured.CreateClient();

        var response = await http.GetAsync($"/api/search/stream?q={Uri.EscapeDataString(Query)}");
        var body = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("OfflineChatClient", body);
        Assert.DoesNotContain("InvalidOperationException", body);
        Assert.DoesNotContain("   at ", body); // stack trace frame prefix
    }
}

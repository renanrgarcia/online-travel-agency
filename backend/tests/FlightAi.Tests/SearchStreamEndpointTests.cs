using System.Diagnostics;
using FlightAi.Api;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace FlightAi.Tests;

/// <summary>
/// One test per eval in docs/specs/tasks/12-search-api-sse-skeleton.md, against a real HTTP request
/// through <see cref="WebApplicationFactory{TEntryPoint}"/> — not a call straight into
/// <see cref="SkeletonEventSource"/>, since the evals are about what actually reaches the wire
/// (headers, framing, timing), which only a real request can prove.
/// </summary>
public class SearchStreamEndpointTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact] // E1 — the contract's baseline
    public async Task E1_ContentTypeIsEventStream()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/search/stream", HttpCompletionOption.ResponseHeadersRead);

        Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact] // E2 — docs/09-lessons-learned.md documents a real double-header bug here
    public async Task E2_ExactlyOneContentTypeHeader()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/search/stream", HttpCompletionOption.ResponseHeadersRead);

        Assert.Single(response.Content.Headers.GetValues("Content-Type"));
    }

    [Fact] // E3 — genuine streaming, not a buffered response pretending; the most common way this silently fails
    public async Task E3_EventsSpacedFiveHundredMillisecondsApart_ArriveIncrementallyNotAllAtOnce()
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/api/search/stream", HttpCompletionOption.ResponseHeadersRead);
        await using var stream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream);

        var stopwatch = Stopwatch.StartNew();
        var dataTimestamps = new List<TimeSpan>();

        string? line;
        while ((line = await reader.ReadLineAsync()) is not null)
        {
            if (line.StartsWith("data:", StringComparison.Ordinal))
                dataTimestamps.Add(stopwatch.Elapsed);
        }

        Assert.Equal(3, dataTimestamps.Count);
        var gapBeforeSecondEvent = dataTimestamps[1] - dataTimestamps[0];
        Assert.True(gapBeforeSecondEvent > TimeSpan.FromMilliseconds(350),
            $"expected ~500ms between the first two events, saw {gapBeforeSecondEvent.TotalMilliseconds}ms -- a buffered response would show ~0ms here");
    }

    [Fact] // E4 — a browser EventSource is unforgiving about framing
    public async Task E4_EventFraming_MatchesTheDocumentedShape()
    {
        using var client = factory.CreateClient();

        var body = await client.GetStringAsync("/api/search/stream");

        // event: <type>\ndata: <payload>\n\n, per docs/06-api-sse-contract.md.
        Assert.Contains("event: tick\ndata: {\"index\":1}\n\n", body);
        Assert.Contains("event: done\ndata: {}\n\n", body);
    }

    [Fact] // E6 — Brazilian-market data will hit this immediately
    public async Task E6_UTF8AccentedPayload_ArrivesIntact()
    {
        using var client = factory.CreateClient();

        var body = await client.GetStringAsync("/api/search/stream");

        Assert.Contains("São Paulo", body);
    }

    [Fact] // E5 — otherwise abandoned searches burn supplier budget in task 13
    public async Task E5_CancelledPartwayThrough_StopsEmittingFurtherEvents()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(250)); // between event 1 (immediate) and event 2 (at 500ms)

        var emitted = new List<string>();
        var ex = await Record.ExceptionAsync(async () =>
        {
            await foreach (var item in SkeletonEventSource.GenerateAsync(cts.Token))
                emitted.Add(item.EventType!);
        });

        Assert.IsType<TaskCanceledException>(ex);
        Assert.Single(emitted); // only the first event had been yielded before cancellation landed mid-delay
    }
}

using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;

namespace FlightAi.Api;

/// <summary>
/// Three hard-coded events, transport only — no pipeline logic. See
/// docs/specs/tasks/12-search-api-sse-skeleton.md. Task 13 replaces this with the real search
/// pipeline's four event types (parsed-intent, supplier-result, ranked-offers, explanation).
/// </summary>
public static class SkeletonEventSource
{
    public static async IAsyncEnumerable<SseItem<string>> GenerateAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        yield return new SseItem<string>("""{"index":1}""", "tick");
        await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);

        // A UTF-8 accented string travels through untouched (task 12 E6) -- the Brazilian market's
        // data will hit this immediately, so it's checked here rather than assumed.
        yield return new SseItem<string>("""{"index":2,"city":"São Paulo"}""", "tick");
        await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);

        yield return new SseItem<string>("{}", "done");
    }
}

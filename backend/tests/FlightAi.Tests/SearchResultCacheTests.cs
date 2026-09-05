using FlightAi.Api;
using FlightAi.Core.Models.Offers;
using Xunit;

namespace FlightAi.Tests;

/// <summary>
/// One test per relevant eval in docs/features/01-backend/tasks/26-paginated-search-results.md against
/// <see cref="SearchResultCache"/> directly -- E1/E2/E4/E5's actual paging behavior is exercised end to
/// end in SearchApiPipelineTests instead; this file covers what only makes sense to test in isolation:
/// the cache's own storage and expiry mechanics.
/// </summary>
public class SearchResultCacheTests
{
    private sealed class FakeClock(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }

    private static FakeClock NewClock() => new(new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero));

    private static CachedOffer SampleOffer(string offerId = "LCC-002") => new(
        new Offer(offerId, 590m, "USD", TimeSpan.FromHours(8), 1, false, 15m, DateTimeOffset.UtcNow.AddHours(2)),
        Rank: 1, Score: 42m);

    [Fact] // baseline -- a stored entry comes back exactly as stored
    public void StoredEntry_IsReturnedByTryGet()
    {
        var cache = new SearchResultCache(TimeSpan.FromMinutes(20), NewClock());
        var searchId = SearchResultCache.NewSearchId();
        var offers = new[] { SampleOffer() };

        cache.Store(searchId, offers);

        Assert.Same(offers, cache.TryGet(searchId));
    }

    [Fact] // task 26 E3 -- an unknown ID was never stored at all
    public void UnknownSearchId_ReturnsNull()
    {
        var cache = new SearchResultCache(TimeSpan.FromMinutes(20), NewClock());

        Assert.Null(cache.TryGet(SearchResultCache.NewSearchId()));
    }

    [Fact] // task 26 E6 -- an entry actually leaves the cache after its TTL, not just fails to resolve
    public void EntryPastTtl_IsEvicted()
    {
        var clock = NewClock();
        var cache = new SearchResultCache(TimeSpan.FromMinutes(20), clock);
        var searchId = SearchResultCache.NewSearchId();
        cache.Store(searchId, [SampleOffer()]);

        clock.Advance(TimeSpan.FromMinutes(20).Add(TimeSpan.FromSeconds(1)));
        var result = cache.TryGet(searchId);

        Assert.Null(result);
        Assert.Equal(0, cache.Count);
    }

    [Fact] // task 26 E6 -- still retrievable one second before the TTL elapses
    public void EntryJustBeforeTtl_IsStillRetrievable()
    {
        var clock = NewClock();
        var cache = new SearchResultCache(TimeSpan.FromMinutes(20), clock);
        var searchId = SearchResultCache.NewSearchId();
        cache.Store(searchId, [SampleOffer()]);

        clock.Advance(TimeSpan.FromMinutes(20).Subtract(TimeSpan.FromSeconds(1)));

        Assert.NotNull(cache.TryGet(searchId));
    }

    [Fact] // a fresh Store on a later search opportunistically purges anything already expired
    public void Store_PurgesAlreadyExpiredEntries()
    {
        var clock = NewClock();
        var cache = new SearchResultCache(TimeSpan.FromMinutes(20), clock);
        var firstId = SearchResultCache.NewSearchId();
        cache.Store(firstId, [SampleOffer()]);

        clock.Advance(TimeSpan.FromMinutes(21));
        cache.Store(SearchResultCache.NewSearchId(), [SampleOffer()]);

        Assert.Equal(1, cache.Count);
    }
}

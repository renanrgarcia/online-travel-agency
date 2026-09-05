using System.Collections.Concurrent;
using FlightAi.Core.Models.Offers;

namespace FlightAi.Api;

/// <summary>One offer as SearchPipeline ranked it, before capping and before a PriceAssertion is
/// attached -- a fresh assertion is issued per request (search or page), never cached, since it's
/// meaningless once its own validity window has passed.</summary>
public sealed record CachedOffer(Offer Offer, int Rank, decimal Score);

/// <summary>
/// Caches one search's full ranked offer list so a "show more" page (task 26) can serve later slices of
/// it without a second call to any supplier -- risking a different result set than what the traveller
/// already saw, since a real supplier's live inventory can shift between calls.
/// <para>
/// In-process only, and deliberately not built on <c>IMemoryCache</c>: a single App Service instance has
/// nothing to distribute a cache across, and this project already has an established pattern for
/// in-process, time-boxed state with deterministic tests -- <see cref="PriceAssertionService"/>-style
/// (technically <c>FlightAi.Core.Services.Pricing.PriceAssertionService</c>) explicit
/// <see cref="TimeProvider"/> injection -- rather than reaching for a caching abstraction this codebase
/// doesn't use anywhere else.
/// </para>
/// </summary>
public sealed class SearchResultCache(TimeSpan ttl, TimeProvider? timeProvider = null)
{
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;
    private readonly ConcurrentDictionary<string, (IReadOnlyList<CachedOffer> Offers, DateTimeOffset ExpiresAt)> _entries = new();

    /// <summary>Generates a fresh ID for a search that's just starting -- before ranking exists yet, so
    /// the <c>search-id</c> SSE event can be emitted immediately after <c>parsed-intent</c> (task 26's
    /// locked decision), well before <see cref="Store"/> below has anything real to put under it.</summary>
    public static string NewSearchId() => Guid.NewGuid().ToString("N");

    /// <summary>Stores one search's full ranked list under an ID <see cref="NewSearchId"/> already
    /// generated and already sent to the client. Also opportunistically purges anything already expired
    /// -- piggybacked here rather than a separate background sweep, since this project's traffic scale
    /// doesn't warrant one and every store is already a natural checkpoint.</summary>
    public void Store(string searchId, IReadOnlyList<CachedOffer> rankedOffers)
    {
        PurgeExpired();
        _entries[searchId] = (rankedOffers, _timeProvider.GetUtcNow() + ttl);
    }

    /// <summary>Null for an unknown or expired ID -- the caller (the paginated-offers endpoint) is the
    /// one that turns that into a 404, not this class's concern.</summary>
    public IReadOnlyList<CachedOffer>? TryGet(string searchId)
    {
        if (!_entries.TryGetValue(searchId, out var entry))
            return null;

        if (entry.ExpiresAt <= _timeProvider.GetUtcNow())
        {
            _entries.TryRemove(searchId, out _);
            return null;
        }

        return entry.Offers;
    }

    /// <summary>Test seam (task 26 E6) -- proves entries actually leave the cache after their TTL,
    /// rather than trusting that the expiry check alone is equivalent to not holding the memory. Public
    /// rather than internal since this codebase has no InternalsVisibleTo wiring to FlightAi.Tests.</summary>
    public int Count => _entries.Count;

    private void PurgeExpired()
    {
        var now = _timeProvider.GetUtcNow();
        foreach (var (key, entry) in _entries)
        {
            if (entry.ExpiresAt <= now)
                _entries.TryRemove(key, out _);
        }
    }
}

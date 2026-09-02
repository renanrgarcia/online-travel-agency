using FlightAi.Core.Models.Offers;

namespace FlightAi.Core.Services.Pricing;

/// <summary>One offer's comparison tokens (task 18). <see cref="PriceDeltaToken"/> / <see cref="DurationDeltaToken"/>
/// are <see langword="null"/> for the top-ranked offer (nothing to compare it against). An offer can
/// genuinely hold more than one superlative at once (the cheapest offer is very often also the
/// fastest), so <see cref="SuperlativeTokens"/> is a list, not a single nullable slot -- collapsing two
/// true superlatives down to one would silently drop a fact this component knows is true.</summary>
public sealed record OfferComparison(string? PriceDeltaToken, string? DurationDeltaToken, IReadOnlyList<string> SuperlativeTokens);

/// <summary>
/// Decides which comparison facts are true among a set of offers, and registers them via
/// <see cref="PriceReferenceStore"/> -- the decision-making half of task 18, kept separate from the
/// store itself (which only ever turns a decision already made into resolved text). Only ever sees the
/// offers actually being explained (task 18 E4): a fact about a ranked offer nobody is shown is not a
/// fact this component has any way to state.
/// </summary>
public static class ComparisonFacts
{
    /// <param name="rankedOffers">In rank order -- the first is the reference point every delta is
    /// computed against (a locked decision: deltas are relative to the top pick, not every pair).</param>
    public static IReadOnlyDictionary<string, OfferComparison> Compute(PriceReferenceStore store, IReadOnlyList<Offer> rankedOffers)
    {
        var priceDelta = new Dictionary<string, string>();
        var durationDelta = new Dictionary<string, string>();

        if (rankedOffers.Count > 0)
        {
            var top = rankedOffers[0];
            foreach (var offer in rankedOffers.Skip(1))
            {
                priceDelta[offer.OfferId] = store.RegisterPriceDelta(top.OfferId, offer.OfferId, offer.Price - top.Price, offer.Currency);
                durationDelta[offer.OfferId] = store.RegisterDurationDelta(top.OfferId, offer.OfferId, offer.Duration - top.Duration);
            }
        }

        var superlatives = new Dictionary<string, List<string>>();
        RegisterIfUniqueMinimum(store, rankedOffers, offer => offer.Price, Superlative.Cheapest, superlatives);
        RegisterIfUniqueMinimum(store, rankedOffers, offer => offer.Duration, Superlative.Fastest, superlatives);
        RegisterIfUniqueMinimum(store, rankedOffers, offer => offer.Stops, Superlative.FewestStops, superlatives);

        var refundableOffers = rankedOffers.Where(offer => offer.Refundable).ToList();
        if (refundableOffers.Count == 1)
            Add(superlatives, refundableOffers[0].OfferId, store.RegisterSuperlative(refundableOffers[0].OfferId, Superlative.OnlyRefundable));

        return rankedOffers.ToDictionary(
            offer => offer.OfferId,
            offer => new OfferComparison(
                priceDelta.GetValueOrDefault(offer.OfferId),
                durationDelta.GetValueOrDefault(offer.OfferId),
                superlatives.TryGetValue(offer.OfferId, out var tokens) ? tokens : []));
    }

    /// <summary>A superlative is only registered when exactly one offer holds the minimum -- a tie
    /// means no offer can honestly be called "the" cheapest/fastest/fewest-stops (task 18 E3): silence
    /// is correct here, a coin-flip winner is not.</summary>
    private static void RegisterIfUniqueMinimum<TValue>(
        PriceReferenceStore store, IReadOnlyList<Offer> offers, Func<Offer, TValue> selector,
        Superlative superlative, Dictionary<string, List<string>> results)
        where TValue : IComparable<TValue>
    {
        if (offers.Count == 0)
            return;

        var minimum = offers.Select(selector).Min();
        var holders = offers.Where(offer => selector(offer).CompareTo(minimum) == 0).ToList();
        if (holders.Count == 1)
            Add(results, holders[0].OfferId, store.RegisterSuperlative(holders[0].OfferId, superlative));
    }

    private static void Add(Dictionary<string, List<string>> results, string offerId, string token)
    {
        if (!results.TryGetValue(offerId, out var tokens))
            results[offerId] = tokens = [];
        tokens.Add(token);
    }
}

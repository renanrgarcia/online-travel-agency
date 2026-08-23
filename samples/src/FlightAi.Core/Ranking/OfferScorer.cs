using FlightAi.Core.Offers;

namespace FlightAi.Core.Ranking;

public sealed record ScoredOffer(Offer Offer, double Score, IReadOnlyDictionary<string, double> FactorScores);

/// <summary>
/// Ranking flights is a scoring function over structured offers, not a chat — a deterministic function
/// you can test, tune, and explain. This is that function. No model call anywhere in it —
/// the same offers, in the same order, produce the same ranking every time, which is the property the
/// unit tests in FlightAi.Tests hold it to.
/// </summary>
public sealed class OfferScorer(ScoringWeights? weights = null)
{
    private readonly ScoringWeights _weights = weights ?? ScoringWeights.Default;

    public IReadOnlyList<ScoredOffer> Rank(IReadOnlyList<Offer> offers)
    {
        if (offers.Count == 0) return [];

        var priceRange = MinMaxRange.Of(offers, o => (double)o.TotalPrice);
        var durationRange = MinMaxRange.Of(offers, o => o.TotalDuration.TotalMinutes);
        var stopsRange = MinMaxRange.Of(offers, o => o.StopCount);
        var layoverRange = MinMaxRange.Of(offers, LayoverPenaltyMinutes);
        var departureRange = MinMaxRange.Of(offers, DepartureDesirabilityPenalty);
        var marginRange = MinMaxRange.Of(offers, o => (double)o.MarginAmount);

        var scored = offers.Select(offer =>
        {
            var factors = new Dictionary<string, double>
            {
                ["price"] = priceRange.Normalize((double)offer.TotalPrice, lowerIsBetter: true) * _weights.Price,
                ["duration"] = durationRange.Normalize(offer.TotalDuration.TotalMinutes, lowerIsBetter: true) * _weights.Duration,
                ["stops"] = stopsRange.Normalize(offer.StopCount, lowerIsBetter: true) * _weights.Stops,
                ["layoverQuality"] = layoverRange.Normalize(LayoverPenaltyMinutes(offer), lowerIsBetter: true) * _weights.LayoverQuality,
                ["departureDesirability"] = departureRange.Normalize(DepartureDesirabilityPenalty(offer), lowerIsBetter: true) * _weights.DepartureDesirability,
                ["fareFlexibility"] = FlexibilityScore(offer.FareRules) * _weights.FareFlexibility,
                ["carrierReliability"] = offer.CarrierOnTimePerformance * _weights.CarrierReliability,
                ["margin"] = marginRange.Normalize((double)offer.MarginAmount, lowerIsBetter: false) * _weights.Margin
            };

            return new ScoredOffer(offer, factors.Values.Sum(), factors);
        });

        // Ties break on OfferId, not on collection order — collection order is not a guaranteed input,
        // and "deterministic" has to mean deterministic regardless of it.
        return scored
            .OrderByDescending(s => s.Score)
            .ThenBy(s => s.Offer.OfferId, StringComparer.Ordinal)
            .ToList();
    }

    private static double LayoverPenaltyMinutes(Offer offer) =>
        offer.Layovers.Sum(l => l.Duration.TotalMinutes + (l.TerminalChange ? 45 : 0));

    private static double DepartureDesirabilityPenalty(Offer offer)
    {
        var firstDeparture = offer.Segments[0].Departure;
        var hour = firstDeparture.Hour + firstDeparture.Minute / 60.0;

        // Comfortable window: 07:00–21:00 local. Distance outside it is the penalty (lower is better).
        if (hour is >= 7 and <= 21) return 0;
        return hour < 7 ? 7 - hour : hour - 21;
    }

    private static double FlexibilityScore(FareRules rules)
    {
        if (rules.Refundable) return 1.0;
        if (rules.ChangeableWithFee) return Math.Clamp(1.0 - (double)rules.ChangeFee / 300.0, 0.1, 0.9);
        return 0.0;
    }

    private readonly record struct MinMaxRange(double Min, double Max)
    {
        public static MinMaxRange Of(IReadOnlyList<Offer> offers, Func<Offer, double> selector)
        {
            var values = offers.Select(selector).ToList();
            return new MinMaxRange(values.Min(), values.Max());
        }

        /// <summary>Min-max normalizes to [0,1]. A zero-width range (every offer ties) scores everyone 1.0 — no signal, no penalty.</summary>
        public double Normalize(double value, bool lowerIsBetter)
        {
            if (Max - Min < 1e-9) return 1.0;
            var normalized = (value - Min) / (Max - Min);
            return lowerIsBetter ? 1.0 - normalized : normalized;
        }
    }
}

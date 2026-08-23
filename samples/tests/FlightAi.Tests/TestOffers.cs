using FlightAi.Core.Offers;

namespace FlightAi.Tests;

internal static class TestOffers
{
    public static Offer Make(
        string id,
        decimal price,
        int stopCount = 0,
        double durationHours = 10,
        double onTimePerformance = 0.8,
        bool refundable = false,
        decimal margin = 20m,
        DateTimeOffset? departure = null)
    {
        var start = departure ?? new DateTimeOffset(2026, 12, 1, 10, 0, 0, TimeSpan.Zero);
        var segments = new List<FlightSegment>();
        var cursor = start;

        for (var i = 0; i <= stopCount; i++)
        {
            var legHours = durationHours / (stopCount + 1);
            var arrival = cursor.AddHours(legHours);
            segments.Add(new FlightSegment("XX", $"XX{i + 1}", "AAA", "BBB", cursor, arrival));
            cursor = arrival.AddMinutes(60);
        }

        var layovers = Enumerable.Range(0, stopCount)
            .Select(_ => new Layover(TimeSpan.FromMinutes(60), TerminalChange: false))
            .ToList();

        return new Offer
        {
            OfferId = id,
            SupplierId = "test-supplier",
            Segments = segments,
            Layovers = layovers,
            TotalPrice = price,
            Currency = "USD",
            MarginAmount = margin,
            FareRules = new FareRules(Refundable: refundable, ChangeableWithFee: !refundable, ChangeFee: 100m),
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(15),
            CarrierOnTimePerformance = onTimePerformance
        };
    }

    public static IReadOnlyList<Offer> One(Offer offer) => [offer];
}

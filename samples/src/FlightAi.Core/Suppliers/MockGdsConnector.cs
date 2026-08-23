using FlightAi.Core.Offers;

namespace FlightAi.Core.Suppliers;

/// <summary>
/// Stands in for an Amadeus/Sabre/Travelport-style adapter: reliable, moderate price, published-fare
/// shape. Wire-level parsing would live in a real adapter — this connector only exists to prove the
/// orchestrator's contract (timeout, budget, partial results) end to end.
/// </summary>
public sealed class MockGdsConnector : ISupplierConnector
{
    public string SupplierId => "gds-amadeus";

    public async Task<IReadOnlyList<Offer>> SearchAsync(SearchRequest request, CancellationToken cancellationToken)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(550), cancellationToken);

        var outbound = new DateTimeOffset(request.DepartureDate.ToDateTime(new TimeOnly(9, 15)), TimeSpan.Zero);

        return
        [
            new Offer
            {
                OfferId = $"GDS-{Guid.NewGuid():N}"[..12],
                SupplierId = SupplierId,
                Segments =
                [
                    new FlightSegment("LH", "LH509", request.Origin, "FRA", outbound, outbound.AddHours(11.5)),
                    new FlightSegment("LH", "LH508", "FRA", request.Destination, outbound.AddHours(12.75), outbound.AddHours(22.5))
                ],
                Layovers = [new Layover(TimeSpan.FromMinutes(75), TerminalChange: false)],
                TotalPrice = 842.00m,
                Currency = "USD",
                MarginAmount = 28.50m,
                FareRules = new FareRules(Refundable: false, ChangeableWithFee: true, ChangeFee: 150m),
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(15),
                CarrierOnTimePerformance = 0.81
            }
        ];
    }
}

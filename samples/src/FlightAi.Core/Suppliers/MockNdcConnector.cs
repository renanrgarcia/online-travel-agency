using FlightAi.Core.Offers;

namespace FlightAi.Core.Suppliers;

/// <summary>
/// Stands in for a direct NDC connection: a personalised, short-lived offer with richer ancillaries
/// and a better direct-relationship margin, priced for this specific search rather than pulled from
/// a published fare pool. Notice the short <c>ExpiresAt</c>.
/// </summary>
public sealed class MockNdcConnector : ISupplierConnector
{
    public string SupplierId => "ndc-turkish";

    public async Task<IReadOnlyList<Offer>> SearchAsync(SearchRequest request, CancellationToken cancellationToken)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(400), cancellationToken);

        var outbound = new DateTimeOffset(request.DepartureDate.ToDateTime(new TimeOnly(23, 40)), TimeSpan.Zero);

        return
        [
            new Offer
            {
                OfferId = $"NDC-{Guid.NewGuid():N}"[..12],
                SupplierId = SupplierId,
                Segments =
                [
                    new FlightSegment("TK", "TK1", request.Origin, "IST", outbound, outbound.AddHours(10.75)),
                    new FlightSegment("TK", "TK88", "IST", request.Destination, outbound.AddHours(12.5), outbound.AddHours(23.25))
                ],
                Layovers = [new Layover(TimeSpan.FromMinutes(105), TerminalChange: true)],
                TotalPrice = 791.00m,
                Currency = "USD",
                MarginAmount = 41.00m,
                FareRules = new FareRules(Refundable: true, ChangeableWithFee: true, ChangeFee: 75m),
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(3),
                CarrierOnTimePerformance = 0.76
            }
        ];
    }
}

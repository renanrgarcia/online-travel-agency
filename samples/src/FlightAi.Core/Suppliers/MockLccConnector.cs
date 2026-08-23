using FlightAi.Core.Offers;

namespace FlightAi.Core.Suppliers;

/// <summary>
/// Stands in for a low-cost-carrier / direct-connect adapter that is having a bad day. The first two
/// calls stall past any reasonable timeout on purpose — this connector exists specifically to drive
/// the orchestrator's timeout and circuit-breaker paths in the demo, exercising both the
/// "one supplier stalls the whole search" case and a look-to-book budget breach.
/// </summary>
public sealed class MockLccConnector : ISupplierConnector
{
    private int _callCount;

    public string SupplierId => "lcc-direct";

    public async Task<IReadOnlyList<Offer>> SearchAsync(SearchRequest request, CancellationToken cancellationToken)
    {
        var call = Interlocked.Increment(ref _callCount);

        // Simulates a supplier degradation: unreasonably slow for its first two calls, then fine.
        // In the demo this connector never actually reaches call 3 — the circuit breaker opens first.
        var delay = call <= 2 ? TimeSpan.FromSeconds(2) : TimeSpan.FromMilliseconds(300);
        await Task.Delay(delay, cancellationToken);

        var outbound = new DateTimeOffset(request.DepartureDate.ToDateTime(new TimeOnly(6, 5)), TimeSpan.Zero);

        return
        [
            new Offer
            {
                OfferId = $"LCC-{Guid.NewGuid():N}"[..12],
                SupplierId = SupplierId,
                Segments =
                [
                    new FlightSegment("W6", "W61", request.Origin, "WAW", outbound, outbound.AddHours(9.5)),
                    new FlightSegment("W6", "W6220", "WAW", request.Destination, outbound.AddHours(13.0), outbound.AddHours(24.5))
                ],
                Layovers = [new Layover(TimeSpan.FromMinutes(210), TerminalChange: true)],
                TotalPrice = 611.00m,
                Currency = "USD",
                MarginAmount = 19.00m,
                FareRules = new FareRules(Refundable: false, ChangeableWithFee: false, ChangeFee: 0m),
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10),
                CarrierOnTimePerformance = 0.68
            }
        ];
    }
}

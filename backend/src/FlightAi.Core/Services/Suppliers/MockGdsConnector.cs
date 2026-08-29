using FlightAi.Core.Models.Offers;

namespace FlightAi.Core.Services.Suppliers;

/// <summary>
/// A deterministic GDS-style mock connector — traditional aggregator shape: broader routing (more
/// stops than NDC's direct-carrier channel), priced and margined between the NDC and LCC extremes.
/// Returns the canonical <see cref="Offer"/> directly; it exists to prove the fan-out orchestrator's
/// contract (task 06), not to parse a real Amadeus/Sabre wire format — see
/// docs/reference/01-architecture-overview.md, which names all three mock connectors explicitly.
/// </summary>
public sealed class MockGdsConnector(TimeSpan? simulatedDelay = null)
    : MockSupplierConnectorBase(simulatedDelay ?? TimeSpan.Zero)
{
    private static readonly DateTimeOffset FixtureExpiresAt = new(2027, 3, 1, 0, 20, 0, TimeSpan.Zero);

    public override string Name => "GDS";

    protected override IReadOnlyList<Offer> BuildOffers() =>
    [
        new Offer("GDS-001", Price: 730m, Currency: "USD", Duration: TimeSpan.FromHours(7), Stops: 1, Refundable: true, Margin: 35m, ExpiresAt: FixtureExpiresAt),
        new Offer("GDS-002", Price: 880m, Currency: "USD", Duration: TimeSpan.FromHours(10), Stops: 2, Refundable: true, Margin: 50m, ExpiresAt: FixtureExpiresAt),
    ];
}

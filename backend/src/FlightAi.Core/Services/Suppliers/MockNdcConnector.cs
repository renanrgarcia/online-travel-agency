using FlightAi.Core.Models.Offers;

namespace FlightAi.Core.Services.Suppliers;

/// <summary>
/// A deterministic NDC-style mock connector — full-service-carrier shape: fewer stops, higher margin,
/// higher price. Returns the canonical <see cref="Offer"/> directly; it exists to prove the fan-out
/// orchestrator's contract (task 06), not to parse a real NDC wire format — see
/// docs/reference/01-architecture-overview.md.
/// </summary>
public sealed class MockNdcConnector(TimeSpan? simulatedDelay = null)
    : MockSupplierConnectorBase(simulatedDelay ?? TimeSpan.Zero)
{
    private static readonly DateTimeOffset FixtureExpiresAt = new(2027, 3, 1, 0, 20, 0, TimeSpan.Zero);

    public override string Name => "NDC";

    protected override IReadOnlyList<Offer> BuildOffers() =>
    [
        new Offer("NDC-001", Price: 820m, Currency: "USD", Duration: TimeSpan.FromHours(6), Stops: 0, Refundable: true, Margin: 60m, ExpiresAt: FixtureExpiresAt),
        new Offer("NDC-002", Price: 650m, Currency: "USD", Duration: TimeSpan.FromHours(9), Stops: 1, Refundable: false, Margin: 45m, ExpiresAt: FixtureExpiresAt),
    ];
}

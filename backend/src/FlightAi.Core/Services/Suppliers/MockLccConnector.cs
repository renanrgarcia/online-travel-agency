using FlightAi.Core.Models.Offers;

namespace FlightAi.Core.Services.Suppliers;

/// <summary>
/// A deterministic LCC-style mock connector — budget-carrier shape: more stops, lower margin, lower
/// price. Returns the canonical <see cref="Offer"/> directly; it exists to prove the fan-out
/// orchestrator's contract (task 06), not to parse a real LCC wire format — see
/// docs/01-architecture-overview.md.
/// </summary>
public sealed class MockLccConnector(TimeSpan? simulatedDelay = null)
    : MockSupplierConnectorBase(simulatedDelay ?? TimeSpan.Zero)
{
    private static readonly DateTimeOffset FixtureExpiresAt = new(2027, 3, 1, 0, 20, 0, TimeSpan.Zero);

    public override string Name => "LCC";

    protected override IReadOnlyList<Offer> BuildOffers() =>
    [
        new Offer("LCC-001", Price: 410m, Currency: "USD", Duration: TimeSpan.FromHours(11), Stops: 2, Refundable: false, Margin: 25m, ExpiresAt: FixtureExpiresAt),
        new Offer("LCC-002", Price: 590m, Currency: "USD", Duration: TimeSpan.FromHours(8), Stops: 1, Refundable: false, Margin: 15m, ExpiresAt: FixtureExpiresAt),
    ];
}

using FlightAi.Core.Interfaces.Suppliers;
using FlightAi.Core.Models.Offers;
using FlightAi.Core.Models.Suppliers;

namespace FlightAi.Core.Services.Suppliers;

/// <summary>
/// Shared plumbing for the mock connectors: simulated latency (honouring cancellation) and the
/// per-connector failure-injection convention. See docs/features/01-backend/tasks/05-mock-supplier-connectors.md.
/// </summary>
public abstract class MockSupplierConnectorBase(TimeSpan simulatedDelay) : ISupplierConnector
{
    public abstract string Name { get; }

    /// <summary>Deterministic, hand-built offers for this connector. Never varies by request beyond
    /// the failure marker — that's what makes E1 (reproducibility) trivially true.</summary>
    protected abstract IReadOnlyList<Offer> BuildOffers();

    public async Task<SupplierSearchResult> SearchAsync(SearchRequest request, CancellationToken cancellationToken)
    {
        try
        {
            if (simulatedDelay > TimeSpan.Zero)
                await Task.Delay(simulatedDelay, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return SupplierSearchResult.Cancelled();
        }

        // Per-connector marker, not a blanket "FAIL-SEARCH": both connectors see the same shared
        // request, so a marker with no connector name would fail every connector identically,
        // contradicting the requirement that failure is per-connector, never global.
        if (request.Destination.Contains($"FAIL-SEARCH-{Name}", StringComparison.Ordinal))
            return SupplierSearchResult.Failure($"{Name} search failed for destination \"{request.Destination}\"");

        return SupplierSearchResult.Success(BuildOffers());
    }
}

using FlightAi.Core.Offers;

namespace FlightAi.Core.Suppliers;

/// <summary>
/// The seam every supplier integration talks through. See
/// docs/specs/tasks/04-supplier-connector-interface.md.
/// </summary>
public interface ISupplierConnector
{
    /// <summary>Stable identity, used as the key in task 06's per-supplier reporting and task 07's
    /// circuit-breaker state.</summary>
    string Name { get; }

    Task<SupplierSearchResult> SearchAsync(SearchRequest request, CancellationToken cancellationToken);
}

using FlightAi.Core.Offers;

namespace FlightAi.Core.Suppliers;

/// <summary>
/// The one interface every supplier sits behind. A GDS, an NDC gateway and an
/// aggregator all implement this the same way — their wire formats never escape the adapter.
/// </summary>
public interface ISupplierConnector
{
    string SupplierId { get; }

    Task<IReadOnlyList<Offer>> SearchAsync(SearchRequest request, CancellationToken cancellationToken);
}

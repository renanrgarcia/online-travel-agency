using FlightAi.Core.Models.Offers;

namespace FlightAi.Core.Models.Suppliers;

public enum SupplierOutcome
{
    /// <summary>Answered cleanly — <see cref="SupplierSearchResult.Offers"/> may be empty; "no
    /// flights" is a valid answer, not a failure.</summary>
    Success,

    /// <summary>Returned some offers, then failed (e.g. failed partway through paging results).
    /// Distinct from <see cref="Failure"/> specifically so those offers are never accidentally
    /// discarded alongside the failure.</summary>
    PartialSuccess,

    /// <summary>Failed outright — zero offers.</summary>
    Failure,

    /// <summary>The caller's <see cref="CancellationToken"/> fired. Distinct from
    /// <see cref="Failure"/> so a timeout is never misattributed to the supplier.</summary>
    Cancelled,
}

/// <summary>
/// Every state a supplier search can end in, representable without throwing or returning null. See
/// docs/features/01-backend/tasks/04-supplier-connector-interface.md. Construct via the factory methods, never the
/// primary constructor directly, so the invariants per <see cref="SupplierOutcome"/> value hold.
/// </summary>
public sealed record SupplierSearchResult(SupplierOutcome Outcome, IReadOnlyList<Offer> Offers, string? FailureReason)
{
    public static SupplierSearchResult Success(IReadOnlyList<Offer> offers) =>
        new(SupplierOutcome.Success, offers, FailureReason: null);

    public static SupplierSearchResult PartialSuccess(IReadOnlyList<Offer> offers, string reason)
    {
        if (offers.Count == 0)
            throw new ArgumentException("PartialSuccess requires at least one offer — use Failure for zero offers.", nameof(offers));

        return new(SupplierOutcome.PartialSuccess, offers, reason);
    }

    public static SupplierSearchResult Failure(string reason) => new(SupplierOutcome.Failure, [], reason);

    public static SupplierSearchResult Cancelled() => new(SupplierOutcome.Cancelled, [], FailureReason: null);
}

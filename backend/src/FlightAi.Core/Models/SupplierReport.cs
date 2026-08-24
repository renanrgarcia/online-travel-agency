namespace FlightAi.Core.Models;

/// <summary>
/// How one connector's participation in a fan-out ended. Deliberately finer-grained than
/// <see cref="SupplierOutcome"/>: "called and failed", "called and timed out", and "never called"
/// are different information for the client (task 13 streams one of these per connector) and for
/// operators, even though several of them count identically toward the circuit breaker.
/// </summary>
public enum SupplierStatus
{
    Succeeded,
    PartialSuccess,

    /// <summary>The supplier itself reported a failure.</summary>
    Failed,

    /// <summary>Exceeded this fan-out's per-connector timeout. Distinct from <see cref="Failed"/> so a
    /// slow supplier is never misreported as a broken one.</summary>
    TimedOut,

    /// <summary>The caller cancelled the whole search. Not the supplier's fault, and deliberately not
    /// counted against it — see task 06's locked decisions.</summary>
    Cancelled,

    /// <summary>Never called: its circuit breaker was open (task 07).</summary>
    SkippedCircuitOpen,

    /// <summary>Never called: the look-to-book budget was exhausted (task 07).</summary>
    SkippedBudgetExhausted,
}

/// <summary>One connector's line in the fan-out's status report.</summary>
public sealed record SupplierReport(string SupplierName, SupplierStatus Status, int OfferCount, string? Reason);

/// <summary>
/// A fan-out's merged offers plus exactly one <see cref="SupplierReport"/> per registered connector.
/// An empty <see cref="Offers"/> list is a valid, successful result — "every supplier failed" is still
/// an answer the API has to be able to stream.
/// </summary>
public sealed record FanOutResult(IReadOnlyList<Offer> Offers, IReadOnlyList<SupplierReport> Reports);

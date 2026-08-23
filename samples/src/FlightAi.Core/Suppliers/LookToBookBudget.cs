namespace FlightAi.Core.Suppliers;

public sealed class LookToBookBudgetExceededException(string supplierId, int limit)
    : Exception($"Look-to-book budget exhausted for supplier '{supplierId}' (limit {limit} shopping calls per session).")
{
    public string SupplierId { get; } = supplierId;
}

/// <summary>
/// Per-session, per-supplier shopping-call budget. Suppliers meter search
/// requests against booking volume — an agent that "tries a few extra date variations" without a
/// hard cap here is a contractual and financial incident, not a UX nicety. This is the counter that
/// belongs on the same dashboard as p95 latency.
/// </summary>
public sealed class LookToBookBudget(int maxCallsPerSupplierPerSession)
{
    private readonly Dictionary<string, int> _callsBySupplier = [];
    private readonly object _gate = new();

    public int MaxCallsPerSupplierPerSession { get; } = maxCallsPerSupplierPerSession;

    /// <summary>Reserves one shopping call before it is made. Throws if the session budget is spent.</summary>
    public void ReserveCall(string supplierId)
    {
        lock (_gate)
        {
            var used = _callsBySupplier.GetValueOrDefault(supplierId);
            if (used >= MaxCallsPerSupplierPerSession)
                throw new LookToBookBudgetExceededException(supplierId, MaxCallsPerSupplierPerSession);

            _callsBySupplier[supplierId] = used + 1;
        }
    }

    public IReadOnlyDictionary<string, int> Snapshot()
    {
        lock (_gate)
            return new Dictionary<string, int>(_callsBySupplier);
    }

    public int TotalCalls()
    {
        lock (_gate)
            return _callsBySupplier.Values.Sum();
    }
}

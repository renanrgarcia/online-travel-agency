using System.Diagnostics;
using FlightAi.Core.Offers;

namespace FlightAi.Core.Suppliers;

public sealed record SupplierResult(
    string SupplierId,
    IReadOnlyList<Offer> Offers,
    bool Succeeded,
    bool BudgetSkipped,
    bool CircuitOpen,
    TimeSpan Elapsed,
    string? Error);

public sealed record FanOutResult(IReadOnlyList<Offer> Offers, IReadOnlyList<SupplierResult> PerSupplier)
{
    public bool IsPartial => PerSupplier.Any(r => !r.Succeeded);
}

/// <summary>
/// Every connector is called in parallel, under its own timeout, counted against the look-to-book
/// budget before the call is even made, so one slow or failing supplier degrades the result to
/// partial data rather than stalling or failing the whole search.
/// </summary>
public sealed class SupplierFanOutOrchestrator
{
    private readonly IReadOnlyList<ISupplierConnector> _connectors;
    private readonly LookToBookBudget _budget;
    private readonly TimeSpan _perSupplierTimeout;
    private readonly Dictionary<string, SupplierCircuitBreaker> _breakers;

    public SupplierFanOutOrchestrator(
        IEnumerable<ISupplierConnector> connectors,
        LookToBookBudget budget,
        TimeSpan? perSupplierTimeout = null,
        int circuitFailureThreshold = 3,
        TimeSpan? circuitOpenDuration = null)
    {
        _connectors = connectors.ToList();
        _budget = budget;
        _perSupplierTimeout = perSupplierTimeout ?? TimeSpan.FromSeconds(2.5);
        _breakers = _connectors.ToDictionary(
            c => c.SupplierId,
            _ => new SupplierCircuitBreaker(circuitFailureThreshold, circuitOpenDuration ?? TimeSpan.FromSeconds(30)));
    }

    /// <param name="onSupplierResult">
    /// Optional callback fired the instant each supplier finishes — success, timeout, budget skip or
    /// circuit-open — rather than waiting for the whole fan-out to settle. Exists so a caller (the API
    /// layer's SSE endpoint) can stream results to a UI as they land instead of only after the slowest
    /// supplier responds. Invoked on whatever thread that supplier's call completes on; keep it fast
    /// and thread-safe — it is not synchronized with the other suppliers' callbacks.
    /// </param>
    public async Task<FanOutResult> SearchAsync(
        SearchRequest request,
        Action<SupplierResult>? onSupplierResult = null,
        CancellationToken cancellationToken = default)
    {
        var tasks = _connectors.Select(async connector =>
        {
            var result = await CallOneAsync(connector, request, cancellationToken);
            onSupplierResult?.Invoke(result);
            return result;
        });

        var results = await Task.WhenAll(tasks);
        var offers = results.SelectMany(r => r.Offers).ToList();
        return new FanOutResult(offers, results);
    }

    private async Task<SupplierResult> CallOneAsync(
        ISupplierConnector connector, SearchRequest request, CancellationToken outerToken)
    {
        var breaker = _breakers[connector.SupplierId];
        var stopwatch = Stopwatch.StartNew();

        if (!breaker.AllowRequest())
            return new SupplierResult(connector.SupplierId, [], false, false, true, stopwatch.Elapsed, "Circuit open — skipped without calling the supplier");

        try
        {
            _budget.ReserveCall(connector.SupplierId);
        }
        catch (LookToBookBudgetExceededException ex)
        {
            return new SupplierResult(connector.SupplierId, [], false, true, false, stopwatch.Elapsed, ex.Message);
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(outerToken);
        timeoutCts.CancelAfter(_perSupplierTimeout);

        try
        {
            var offers = await connector.SearchAsync(request, timeoutCts.Token);
            breaker.RecordSuccess();
            return new SupplierResult(connector.SupplierId, offers, true, false, false, stopwatch.Elapsed, null);
        }
        catch (OperationCanceledException) when (!outerToken.IsCancellationRequested)
        {
            breaker.RecordFailure();
            return new SupplierResult(connector.SupplierId, [], false, false, false, stopwatch.Elapsed,
                $"Timed out after {_perSupplierTimeout.TotalSeconds:0.0}s");
        }
        catch (Exception ex)
        {
            breaker.RecordFailure();
            return new SupplierResult(connector.SupplierId, [], false, false, false, stopwatch.Elapsed, ex.Message);
        }
    }
}

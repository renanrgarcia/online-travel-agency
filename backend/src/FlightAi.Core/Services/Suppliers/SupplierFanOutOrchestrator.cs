using System.Runtime.CompilerServices;
using FlightAi.Core.Interfaces.Suppliers;
using FlightAi.Core.Models.Offers;
using FlightAi.Core.Models.Suppliers;

namespace FlightAi.Core.Services.Suppliers;

/// <summary>
/// Calls every registered connector concurrently, bounds each one with its own timeout, and degrades
/// to partial results instead of failing the whole search. See
/// docs/features/01-backend/tasks/06-supplier-fan-out-orchestrator.md.
/// <para>
/// Every connector's timeout, look-to-book budget, and circuit breaker come from
/// <paramref name="policies"/> — keyed by <see cref="ISupplierConnector.Name"/>, one
/// <see cref="SupplierPolicy"/> per connector, all fields required. Real suppliers carry genuinely
/// different commercial terms; earlier versions of this orchestrator shared one
/// timeout/budget/breaker across every connector, and a later version made budget/breaker optional
/// per connector — both under-modelled the reality that a missing budget is a real financial risk,
/// per docs/reference/03-suppliers-and-budget.md. Every connector gets a real, non-optional budget and breaker
/// now; see docs/features/01-backend/tasks/07-look-to-book-budget-and-circuit-breaker.md.
/// </para>
/// </summary>
public sealed class SupplierFanOutOrchestrator
{
    private readonly IReadOnlyList<ISupplierConnector> _connectors;
    private readonly IReadOnlyDictionary<string, SupplierPolicy> _policies;
    private readonly Dictionary<string, LookToBookBudget> _budgets = [];
    private readonly Dictionary<string, SupplierCircuitBreaker> _breakers = [];

    public SupplierFanOutOrchestrator(
        IReadOnlyList<ISupplierConnector> connectors,
        IReadOnlyDictionary<string, SupplierPolicy> policies,
        TimeProvider? timeProvider = null)
    {
        _connectors = connectors;
        _policies = policies;

        foreach (var connector in connectors)
        {
            var policy = PolicyFor(connector);
            _budgets[connector.Name] = new LookToBookBudget(policy.BudgetCeiling, policy.BudgetWindow, timeProvider);
            _breakers[connector.Name] = new SupplierCircuitBreaker(policy.BreakerFailureThreshold, policy.BreakerCooldown, timeProvider);
        }
    }

    /// <summary>
    /// Yields each connector's outcome as it actually finishes, not in registration order — the
    /// mechanism task 13's SSE endpoint needs to emit one <c>supplier-result</c> event per connector as
    /// each really completes, rather than batched at the end. Completion order is not reproducible run
    /// to run (it depends on real timing), so a caller that needs a stable, reproducible input for
    /// ranking (task 03) must sort the merged offers itself once streaming completes — see
    /// <c>SearchPipeline.RunAsync</c>, the one production consumer of this method.
    /// </summary>
    public async IAsyncEnumerable<(IReadOnlyList<Offer> Offers, SupplierReport Report)> SearchStreamingAsync(
        SearchRequest request, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Materialised before awaiting so every connector is genuinely in flight at once; awaiting
        // inside the projection would run them one after another.
        var invocations = _connectors
            .Select(connector => InvokeAsync(connector, request, cancellationToken))
            .ToList();

        await foreach (var completed in Task.WhenEach(invocations))
            yield return await completed;
    }

    private async Task<(IReadOnlyList<Offer> Offers, SupplierReport Report)> InvokeAsync(
        ISupplierConnector connector, SearchRequest request, CancellationToken cancellationToken)
    {
        var policy = PolicyFor(connector);
        var breaker = _breakers[connector.Name];
        var budget = _budgets[connector.Name];

        if (breaker.IsOpen)
            return Skipped(connector, SupplierStatus.SkippedCircuitOpen, "circuit open");

        // Checked after the breaker so a supplier that was never going to be called doesn't spend
        // budget that a healthy one could have used.
        if (!budget.TryConsume())
            return Skipped(connector, SupplierStatus.SkippedBudgetExhausted, "look-to-book budget exhausted");

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(policy.Timeout);

        SupplierSearchResult result;
        try
        {
            result = await connector.SearchAsync(request, timeoutSource.Token);
        }
        catch (Exception ex)
        {
            // Task 04's contract says connectors return failures rather than throwing. This orchestrator
            // doesn't get to assume every future connector honours that.
            breaker.RecordFailure();
            return Skipped(connector, SupplierStatus.Failed, ex.Message);
        }

        return Translate(connector, result, cancellationToken, breaker, policy.Timeout);
    }

    private static (IReadOnlyList<Offer> Offers, SupplierReport Report) Translate(
        ISupplierConnector connector, SupplierSearchResult result, CancellationToken cancellationToken,
        SupplierCircuitBreaker breaker, TimeSpan timeout)
    {
        switch (result.Outcome)
        {
            case SupplierOutcome.Success:
                breaker.RecordSuccess();
                return (result.Offers, new SupplierReport(connector.Name, SupplierStatus.Succeeded, result.Offers.Count, null));

            case SupplierOutcome.PartialSuccess:
                // A supplier that answered at all is alive, so this resets the breaker's failure run
                // even though something went wrong partway.
                breaker.RecordSuccess();
                return (result.Offers, new SupplierReport(connector.Name, SupplierStatus.PartialSuccess, result.Offers.Count, result.FailureReason));

            case SupplierOutcome.Failure:
                breaker.RecordFailure();
                return Skipped(connector, SupplierStatus.Failed, result.FailureReason);

            case SupplierOutcome.Cancelled:
                // The connector saw one linked token and can't tell whose cancellation it was. Only
                // the orchestrator knows, so only the orchestrator can attribute it correctly.
                if (cancellationToken.IsCancellationRequested)
                    return Skipped(connector, SupplierStatus.Cancelled, null);

                breaker.RecordFailure();
                return Skipped(connector, SupplierStatus.TimedOut, $"exceeded {timeout.TotalMilliseconds:F0}ms");

            default:
                throw new ArgumentOutOfRangeException(nameof(result), result.Outcome, "unhandled supplier outcome");
        }
    }

    private SupplierPolicy PolicyFor(ISupplierConnector connector) =>
        _policies.TryGetValue(connector.Name, out var policy)
            ? policy
            : throw new ArgumentException($"no SupplierPolicy registered for connector \"{connector.Name}\"", nameof(connector));

    private static (IReadOnlyList<Offer> Offers, SupplierReport Report) Skipped(
        ISupplierConnector connector, SupplierStatus status, string? reason) =>
        ([], new SupplierReport(connector.Name, status, OfferCount: 0, reason));
}

using FlightAi.Core.Interfaces;
using FlightAi.Core.Models;

namespace FlightAi.Core.Services;

/// <summary>
/// Calls every registered connector concurrently, bounds each one with its own timeout, and degrades
/// to partial results instead of failing the whole search. See
/// docs/specs/tasks/06-supplier-fan-out-orchestrator.md.
/// <para>
/// <paramref name="budget"/> and <paramref name="breaker"/> are the task 07 guardrails. Both are
/// optional: passing neither gives a plain fan-out, which is what task 06's own evals exercise.
/// </para>
/// </summary>
public sealed class SupplierFanOutOrchestrator(
    IReadOnlyList<ISupplierConnector> connectors,
    TimeSpan perConnectorTimeout,
    LookToBookBudget? budget = null,
    SupplierCircuitBreaker? breaker = null)
{
    public async Task<FanOutResult> SearchAsync(SearchRequest request, CancellationToken cancellationToken)
    {
        // Materialised before awaiting so every connector is genuinely in flight at once; awaiting
        // inside the projection would run them one after another.
        var invocations = connectors
            .Select(connector => InvokeAsync(connector, request, cancellationToken))
            .ToList();

        var outcomes = await Task.WhenAll(invocations);

        // Task.WhenAll preserves input order, so this is connector registration order; offers within
        // one connector are ordered by ID. Both halves matter -- task 03's ranking has to receive a
        // stable input for task 08's output to be reproducible.
        var offers = outcomes
            .SelectMany(outcome => outcome.Offers.OrderBy(offer => offer.OfferId, StringComparer.Ordinal))
            .ToList();

        return new FanOutResult(offers, [.. outcomes.Select(outcome => outcome.Report)]);
    }

    private async Task<(IReadOnlyList<Offer> Offers, SupplierReport Report)> InvokeAsync(
        ISupplierConnector connector, SearchRequest request, CancellationToken cancellationToken)
    {
        if (breaker is not null && breaker.IsOpen(connector.Name))
            return Skipped(connector, SupplierStatus.SkippedCircuitOpen, "circuit open");

        // Checked after the breaker so a supplier that was never going to be called doesn't spend
        // budget that a healthy one could have used.
        if (budget is not null && !budget.TryConsume())
            return Skipped(connector, SupplierStatus.SkippedBudgetExhausted, "look-to-book budget exhausted");

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(perConnectorTimeout);

        SupplierSearchResult result;
        try
        {
            result = await connector.SearchAsync(request, timeoutSource.Token);
        }
        catch (Exception ex)
        {
            // Task 04's contract says connectors return failures rather than throwing. This orchestrator
            // doesn't get to assume every future connector honours that.
            breaker?.RecordFailure(connector.Name);
            return Skipped(connector, SupplierStatus.Failed, ex.Message);
        }

        return Translate(connector, result, cancellationToken);
    }

    private (IReadOnlyList<Offer> Offers, SupplierReport Report) Translate(
        ISupplierConnector connector, SupplierSearchResult result, CancellationToken cancellationToken)
    {
        switch (result.Outcome)
        {
            case SupplierOutcome.Success:
                breaker?.RecordSuccess(connector.Name);
                return (result.Offers, new SupplierReport(connector.Name, SupplierStatus.Succeeded, result.Offers.Count, null));

            case SupplierOutcome.PartialSuccess:
                // A supplier that answered at all is alive, so this resets the breaker's failure run
                // even though something went wrong partway.
                breaker?.RecordSuccess(connector.Name);
                return (result.Offers, new SupplierReport(connector.Name, SupplierStatus.PartialSuccess, result.Offers.Count, result.FailureReason));

            case SupplierOutcome.Failure:
                breaker?.RecordFailure(connector.Name);
                return Skipped(connector, SupplierStatus.Failed, result.FailureReason);

            case SupplierOutcome.Cancelled:
                // The connector saw one linked token and can't tell whose cancellation it was. Only
                // the orchestrator knows, so only the orchestrator can attribute it correctly.
                if (cancellationToken.IsCancellationRequested)
                    return Skipped(connector, SupplierStatus.Cancelled, null);

                breaker?.RecordFailure(connector.Name);
                return Skipped(connector, SupplierStatus.TimedOut, $"exceeded {perConnectorTimeout.TotalMilliseconds:F0}ms");

            default:
                throw new ArgumentOutOfRangeException(nameof(result), result.Outcome, "unhandled supplier outcome");
        }
    }

    private static (IReadOnlyList<Offer> Offers, SupplierReport Report) Skipped(
        ISupplierConnector connector, SupplierStatus status, string? reason) =>
        ([], new SupplierReport(connector.Name, status, OfferCount: 0, reason));
}

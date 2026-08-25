namespace FlightAi.Core.Services.Suppliers;

/// <summary>
/// Stops calling a connector that has failed <c>failureThreshold</c> times in a row, for
/// <c>cooldown</c>, rather than spending the per-connector timeout on it every single search. State is
/// per connector name — one dead supplier never silences the others.
/// <para>
/// Hand-rolled on purpose so its behaviour is readable in one small file; reach for Polly in a real
/// service rather than reimplementing this (docs/01-architecture-overview.md). State is in-memory and
/// per-process, with the same limitation noted on <see cref="LookToBookBudget"/>.
/// </para>
/// </summary>
public sealed class SupplierCircuitBreaker(int failureThreshold, TimeSpan cooldown, TimeProvider? timeProvider = null)
{
    private sealed class BreakerState
    {
        public int ConsecutiveFailures;
        public DateTimeOffset? OpenedAt;
    }

    private readonly TimeProvider _clock = timeProvider ?? TimeProvider.System;
    private readonly Lock _gate = new();
    private readonly Dictionary<string, BreakerState> _states = new(StringComparer.Ordinal);

    /// <summary>
    /// Whether this connector should be skipped. Closes the circuit as a side effect once the cooldown
    /// has elapsed, so recovery needs no separate timer or background sweep.
    /// </summary>
    public bool IsOpen(string supplierName)
    {
        lock (_gate)
        {
            if (!_states.TryGetValue(supplierName, out var state) || state.OpenedAt is null)
                return false;

            if (_clock.GetUtcNow() - state.OpenedAt.Value < cooldown)
                return true;

            state.OpenedAt = null;
            state.ConsecutiveFailures = 0;
            return false;
        }
    }

    /// <summary>Resets the failure run — the threshold counts <em>consecutive</em> failures.</summary>
    public void RecordSuccess(string supplierName)
    {
        lock (_gate)
        {
            var state = StateFor(supplierName);
            state.ConsecutiveFailures = 0;
            state.OpenedAt = null;
        }
    }

    /// <summary>
    /// Counts one failure toward the threshold, opening the circuit on reaching it. Timeouts are
    /// recorded here too: a supplier that always times out is as unusable as one that errors, even
    /// though the two are reported differently to the client.
    /// </summary>
    public void RecordFailure(string supplierName)
    {
        lock (_gate)
        {
            var state = StateFor(supplierName);
            state.ConsecutiveFailures++;

            if (state.ConsecutiveFailures >= failureThreshold)
                state.OpenedAt = _clock.GetUtcNow();
        }
    }

    private BreakerState StateFor(string supplierName)
    {
        if (!_states.TryGetValue(supplierName, out var state))
        {
            state = new BreakerState();
            _states[supplierName] = state;
        }

        return state;
    }
}

namespace FlightAi.Core.Services.Suppliers;

/// <summary>
/// Stops calling a connector that has failed <c>failureThreshold</c> times in a row, for
/// <c>cooldown</c>, rather than spending its timeout on it every single search. One instance is scoped
/// to exactly one connector — <see cref="Services.Suppliers.SupplierFanOutOrchestrator"/> constructs
/// one per connector from its <see cref="Models.Suppliers.SupplierPolicy"/>, so one dead supplier can
/// never affect another's state; there is no shared dictionary to isolate keys within.
/// <para>
/// Hand-rolled on purpose so its behaviour is readable in one small file; reach for Polly in a real
/// service rather than reimplementing this (docs/reference/01-architecture-overview.md). State is in-memory and
/// per-process — a restart resets it, and two instances of the host don't share one.
/// </para>
/// </summary>
public sealed class SupplierCircuitBreaker(int failureThreshold, TimeSpan cooldown, TimeProvider? timeProvider = null)
{
    private readonly TimeProvider _clock = timeProvider ?? TimeProvider.System;
    private readonly Lock _gate = new();
    private int _consecutiveFailures;
    private DateTimeOffset? _openedAt;

    /// <summary>
    /// Whether this connector should currently be skipped. Closes the circuit as a side effect once
    /// the cooldown has elapsed, so recovery needs no separate timer or background sweep.
    /// </summary>
    public bool IsOpen
    {
        get
        {
            lock (_gate)
            {
                if (_openedAt is null)
                    return false;

                if (_clock.GetUtcNow() - _openedAt.Value < cooldown)
                    return true;

                _openedAt = null;
                _consecutiveFailures = 0;
                return false;
            }
        }
    }

    /// <summary>Resets the failure run — the threshold counts <em>consecutive</em> failures.</summary>
    public void RecordSuccess()
    {
        lock (_gate)
        {
            _consecutiveFailures = 0;
            _openedAt = null;
        }
    }

    /// <summary>
    /// Counts one failure toward the threshold, opening the circuit on reaching it. Timeouts are
    /// recorded here too: a supplier that always times out is as unusable as one that errors, even
    /// though the two are reported differently to the client.
    /// </summary>
    public void RecordFailure()
    {
        lock (_gate)
        {
            _consecutiveFailures++;

            if (_consecutiveFailures >= failureThreshold)
                _openedAt = _clock.GetUtcNow();
        }
    }
}

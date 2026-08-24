namespace FlightAi.Core.Services;

/// <summary>
/// A ceiling on supplier search calls per rolling window. Suppliers meter search volume against actual
/// bookings ("look to book"), so unchecked searching is a real commercial constraint, not just a
/// performance one. See docs/03-suppliers-and-budget.md.
/// <para>
/// State is in-memory and per-process: a restart resets the budget, and two instances don't share one.
/// That's a deliberate limitation for this system (docs/01-architecture-overview.md keeps Redis out on
/// purpose) and would need revisiting before this metered anything real.
/// </para>
/// </summary>
public sealed class LookToBookBudget(int ceiling, TimeSpan window, TimeProvider? timeProvider = null)
{
    private readonly TimeProvider _clock = timeProvider ?? TimeProvider.System;
    private readonly Lock _gate = new();
    private DateTimeOffset _windowStart;
    private bool _windowStarted;
    private int _consumed;

    /// <summary>
    /// Consumes one call from the budget, returning false if the ceiling for the current window is
    /// already reached. Callers report the refusal rather than throwing — consistent with task 04's
    /// "failures are returned, not thrown."
    /// </summary>
    public bool TryConsume()
    {
        lock (_gate)
        {
            var now = _clock.GetUtcNow();

            if (!_windowStarted)
            {
                _windowStart = now;
                _windowStarted = true;
            }
            else if (now - _windowStart >= window)
            {
                _windowStart = now;
                _consumed = 0;
            }

            if (_consumed >= ceiling)
                return false;

            _consumed++;
            return true;
        }
    }
}

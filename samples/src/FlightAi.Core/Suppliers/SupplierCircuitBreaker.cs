namespace FlightAi.Core.Suppliers;

public enum CircuitState { Closed, Open, HalfOpen }

/// <summary>
/// A minimal consecutive-failure circuit breaker. Deliberately hand-rolled rather than pulling in a
/// resilience library — in a real service reach for Polly instead of this; it exists here so the
/// fan-out orchestrator's behaviour stays inspectable in a sample this size.
/// </summary>
public sealed class SupplierCircuitBreaker(int failureThreshold, TimeSpan openDuration)
{
    private readonly object _gate = new();
    private int _consecutiveFailures;
    private DateTimeOffset _openedAt = DateTimeOffset.MinValue;
    private CircuitState _state = CircuitState.Closed;

    public CircuitState State
    {
        get { lock (_gate) return Evaluate(); }
    }

    public bool AllowRequest()
    {
        lock (_gate) return Evaluate() != CircuitState.Open;
    }

    public void RecordSuccess()
    {
        lock (_gate)
        {
            _consecutiveFailures = 0;
            _state = CircuitState.Closed;
        }
    }

    public void RecordFailure()
    {
        lock (_gate)
        {
            _consecutiveFailures++;
            if (_consecutiveFailures >= failureThreshold)
            {
                _state = CircuitState.Open;
                _openedAt = DateTimeOffset.UtcNow;
            }
        }
    }

    private CircuitState Evaluate()
    {
        if (_state == CircuitState.Open && DateTimeOffset.UtcNow - _openedAt >= openDuration)
            _state = CircuitState.HalfOpen;
        return _state;
    }
}

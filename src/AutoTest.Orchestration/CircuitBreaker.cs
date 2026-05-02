using System.Collections.Concurrent;

namespace AutoTest.Orchestration;

internal class CircuitBreaker
{
    private readonly ConcurrentDictionary<string, CircuitState> _states = new();

    public bool IsOpen(string target)
    {
        if (_states.TryGetValue(target, out var state))
            return state.IsOpen();
        return false;
    }

    public void RecordFailure(string target)
    {
        var state = _states.GetOrAdd(target, _ => new CircuitState());
        state.RecordFailure();
    }

    public void RecordSuccess(string target)
    {
        var state = _states.GetOrAdd(target, _ => new CircuitState());
        state.Reset();
    }
}

internal class CircuitState
{
    private int _failureCount;
    private DateTime? _lastFailureAt;

    private const int Threshold = 5;
    private const int ResetMs = 60_000;

    public bool IsOpen()
    {
        if (_failureCount < Threshold) return false;
        if (_lastFailureAt == null) return false;
        if ((DateTime.UtcNow - _lastFailureAt.Value).TotalMilliseconds > ResetMs)
            return false;
        return true;
    }

    public void RecordFailure()
    {
        _failureCount++;
        _lastFailureAt = DateTime.UtcNow;
    }

    public void Reset()
    {
        _failureCount = 0;
        _lastFailureAt = null;
    }
}

using System.Collections.Concurrent;
using AutoTest.Application;

namespace AutoTest.Infrastructure;

public sealed class MonitorExecutionCoordinator : IMonitorExecutionCoordinator
{
    private readonly ConcurrentDictionary<Guid, byte> _inFlight = new();

    public bool TryBegin(Guid monitorId) => _inFlight.TryAdd(monitorId, 0);

    public void End(Guid monitorId) => _inFlight.TryRemove(monitorId, out _);
}

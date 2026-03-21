using System;

namespace AutoTest.Core.Log;

public class ExecutionLog : IAggregateRoot
{
    public Guid Id { get; private set; }
    public Guid MonitorId { get; private set; }
    public DateTime Timestamp { get; private set; }
    public ExecutionResult Result { get; private set; }

    public ExecutionLog(Guid id, Guid monitorId, DateTime timestamp, ExecutionResult result)
    {
        Id = id;
        MonitorId = monitorId;
        Timestamp = timestamp;
        Result = result;
    }
}

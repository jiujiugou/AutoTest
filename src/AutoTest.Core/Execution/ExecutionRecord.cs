namespace AutoTest.Core.Execution;

public class ExecutionRecord : IAggregateRoot
{
    public Guid Id { get; private set; }
    public Guid MonitorId { get; private set; }
    public MonitorStatus Status { get; private set; }
    public DateTime StartedAt { get; private set; }
    public DateTime? FinishedAt { get; private set; }
    public bool IsExecutionSuccess { get; private set; }
    public string? ErrorMessage { get; private set; }
    public string ResultType { get; private set; }
    public string ResultJson { get; private set; }

    public ExecutionRecord()
    {
        ResultType = null!;
        ResultJson = null!;
    }

    public ExecutionRecord(
        Guid id,
        Guid monitorId,
        MonitorStatus status,
        DateTime startedAt,
        DateTime? finishedAt,
        bool isExecutionSuccess,
        string? errorMessage,
        string resultType,
        string resultJson)
    {
        Id = id;
        MonitorId = monitorId;
        Status = status;
        StartedAt = startedAt;
        FinishedAt = finishedAt;
        IsExecutionSuccess = isExecutionSuccess;
        ErrorMessage = errorMessage;
        ResultType = resultType;
        ResultJson = resultJson;
    }
}

namespace AutoTest.Core.Execution;

/// <summary>
/// 一次监控任务执行的持久化记录。
/// </summary>
/// <remarks>
/// 用于审计与回溯：记录执行时间、执行状态、错误信息，以及执行结果的类型与 JSON 内容。
/// </remarks>
public class ExecutionRecord : IAggregateRoot
{
    /// <summary>
    /// 执行记录 ID。
    /// </summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// 关联的监控任务 ID。
    /// </summary>
    public Guid MonitorId { get; private set; }

    /// <summary>
    /// 监控任务状态快照（执行结束时的状态）。
    /// </summary>
    public MonitorStatus Status { get; private set; }

    /// <summary>
    /// 开始执行时间（UTC）。
    /// </summary>
    public DateTime StartedAt { get; private set; }

    /// <summary>
    /// 结束执行时间（UTC）。
    /// </summary>
    public DateTime? FinishedAt { get; private set; }

    /// <summary>
    /// 执行阶段是否成功（不代表断言通过）。
    /// </summary>
    public bool IsExecutionSuccess { get; private set; }

    /// <summary>
    /// 错误信息（执行失败或异常时）。
    /// </summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>
    /// 结果类型标识（通常为结果对象的类型名）。
    /// </summary>
    public string ResultType { get; private set; }

    /// <summary>
    /// 结果 JSON（序列化后的执行结果内容）。
    /// </summary>
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

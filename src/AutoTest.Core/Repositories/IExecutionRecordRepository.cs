using System.Data;
using AutoTest.Core.Assertion;
using AutoTest.Core.Execution;

namespace AutoTest.Core.Abstraction;

/// <summary>
/// 监控任务执行统计。
/// </summary>
public sealed record MonitorExecutionStats(
    int Total,
    int Success,
    int Fail,
    DateTime? FirstStartedAt,
    DateTime? LastStartedAt);

/// <summary>
/// 某个错误信息的聚合统计。
/// </summary>
public sealed record MonitorErrorStat(
    string ErrorMessage,
    int Count,
    DateTime LastOccurredAt);

/// <summary>
/// 执行记录仓储接口。
/// </summary>
/// <remarks>
/// 用于持久化 <see cref="ExecutionRecord"/> 以及断言结果，并提供查询与统计能力。
/// </remarks>
public interface IExecutionRecordRepository
{
    /// <summary>
    /// 新增执行记录。
    /// </summary>
    Task AddAsync(ExecutionRecord record, IDbTransaction? tx = null);

    /// <summary>
    /// 尝试以幂等键创建一条“运行中”的执行记录。
    /// </summary>
    Task<bool> TryAddRunningAsync(
        Guid executionId,
        Guid monitorId,
        DateTime startedAtUtc,
        string? idempotencyKey,
        string lockedBy,
        DateTime heartbeatAtUtc,
        IDbTransaction tx);

    /// <summary>
    /// 获取指定幂等键对应的执行记录 ID。
    /// </summary>
    Task<Guid?> GetIdByIdempotencyKeyAsync(string idempotencyKey);

    /// <summary>
    /// 更新执行心跳时间。
    /// </summary>
    Task UpdateHeartbeatAsync(Guid executionId, string lockedBy, DateTime heartbeatAtUtc, CancellationToken cancellationToken);

    /// <summary>
    /// 更新执行记录的最终结果（结束时间/状态/结果 JSON 等）。
    /// </summary>
    Task UpdateCompletionAsync(
        Guid executionId,
        int status,
        DateTime finishedAtUtc,
        bool isExecutionSuccess,
        string? errorMessage,
        string resultType,
        string resultJson,
        IDbTransaction tx);

    /// <summary>
    /// 批量新增断言结果。
    /// </summary>
    Task AddAssertionResultsAsync(Guid executionId, IEnumerable<AssertionResult> results, IDbTransaction? tx = null);

    /// <summary>
    /// 获取某个监控任务的最新一条执行记录。
    /// </summary>
    Task<ExecutionRecord?> GetLatestByMonitorIdAsync(Guid monitorId);

    /// <summary>
    /// 获取某个监控任务的历史执行记录。
    /// </summary>
    Task<IEnumerable<ExecutionRecord>> GetByMonitorIdAsync(Guid monitorId, int take = 20);

    /// <summary>
    /// 获取某次执行关联的断言结果列表。
    /// </summary>
    Task<IEnumerable<AssertionResult>> GetAssertionResultsAsync(Guid executionId);

    /// <summary>
    /// 获取某个监控任务的执行统计。
    /// </summary>
    Task<MonitorExecutionStats> GetMonitorExecutionStatsAsync(Guid monitorId);

    /// <summary>
    /// 获取某个监控任务最常见的错误信息统计（Top N）。
    /// </summary>
    Task<IEnumerable<MonitorErrorStat>> GetTopErrorStatsAsync(Guid monitorId, int take = 10);
}


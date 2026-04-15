using AutoTest.Application.Dto;
using AutoTest.Core;
using AutoTest.Core.Abstraction;
using AutoTest.Core.Assertion;
using AutoTest.Core.Execution;

namespace AutoTest.Application;

/// <summary>
/// 监控任务应用服务。
/// </summary>
/// <remarks>
/// 提供监控任务的增删改查、执行记录查询、调度配置管理等用例能力。
/// </remarks>
public interface IMonitorService
{
    /// <summary>
    /// 创建一个新的监控任务。
    /// </summary>
    Task<Guid> AddAsync(MonitorDto dto);

    /// <summary>
    /// 更新已有监控任务。
    /// </summary>
    Task UpdateAsync(Guid id, MonitorDto dto);

    /// <summary>
    /// 删除监控任务。
    /// </summary>
    Task DeleteAsync(Guid id);

    /// <summary>
    /// 根据 ID 获取监控任务（用于业务查询）。
    /// </summary>
    Task<MonitorEntity?> GetByIdAsync(Guid id);

    /// <summary>
    /// 获取监控任务列表。
    /// </summary>
    Task<IEnumerable<MonitorEntity>> ListAsync(int take = 50);

    /// <summary>
    /// 获取某个监控任务的最新一次执行记录。
    /// </summary>
    Task<ExecutionRecord?> GetLatestExecutionAsync(Guid monitorId);

    /// <summary>
    /// 获取某个监控任务的执行记录列表。
    /// </summary>
    Task<IEnumerable<ExecutionRecord>> GetExecutionsAsync(Guid monitorId, int take = 20);

    /// <summary>
    /// 获取某次执行的断言结果列表。
    /// </summary>
    Task<IEnumerable<AssertionResult>> GetExecutionAssertionResultsAsync(Guid executionId);

    /// <summary>
    /// 启用/禁用监控任务。
    /// </summary>
    Task SetEnabledAsync(Guid id, bool isEnabled);

    /// <summary>
    /// 设置任务调度信息（每日执行与限次等）。
    /// </summary>
    Task SetScheduleAsync(Guid id, bool autoDailyEnabled, string? autoDailyTime, int? maxRuns);

    /// <summary>
    /// 获取任务调度信息。
    /// </summary>
    Task<(bool AutoDailyEnabled, string? AutoDailyTime, int? MaxRuns, int ExecutedCount)> GetScheduleAsync(Guid id);

    /// <summary>
    /// 自增自动执行次数，并在达到最大次数后自动禁用自动调度。
    /// </summary>
    Task<bool> IncrementAutoExecutedCountAndDisableIfReachedAsync(Guid id);

    /// <summary>
    /// 获取监控任务运行统计信息（执行统计 + Top 错误）。
    /// </summary>
    Task<(MonitorExecutionStats Stats, IEnumerable<MonitorErrorStat> TopErrors)> GetMonitorRuntimeStatsAsync(Guid monitorId, int takeTopErrors = 10);

    /// <summary>
    /// 尝试启动一次执行（幂等）：如果幂等键已存在则返回 Started=false；否则将 Monitor 标记为 Running 并创建 Running 的执行记录。
    /// </summary>
    Task<(bool Started, Guid ExecutionId, DateTime StartedAtUtc)> TryStartExecutionAsync(Guid monitorId, string? idempotencyKey, string lockedBy);
}

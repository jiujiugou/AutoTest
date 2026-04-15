using System;

namespace AutoTest.Core.Execution;

public interface IOrchestrator
{
    /// <summary>
    /// 尝试执行一个任务（判定任务状态、是否启用、是否正在运行）
    /// </summary>
    Task<ExecutionResult> TryExecuteAsync(MonitorEntity monitor, Guid executionId, DateTime startedAtUtc, string lockedBy);

}

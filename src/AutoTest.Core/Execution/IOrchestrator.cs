using System;

namespace AutoTest.Core.Execution;

public interface IOrchestrator
{
    /// <summary>
    /// 尝试执行一个任务（判定任务状态、是否启用、是否正在运行）
    /// </summary>
    Task<ExecutionResult> TryExecuteAsync(MonitorEntity monitor);

    /// <summary>
    /// 批量执行任务（可选，默认可以用 foreach 调用 TryExecuteAsync）
    /// </summary>
    Task<IEnumerable<ExecutionResult>> TryExecuteAllAsync(IEnumerable<MonitorEntity> monitors);
}

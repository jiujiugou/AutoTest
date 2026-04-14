using AutoTest.Core;

namespace AutoTest.Application.ExecutionPipeline;

/// <summary>
/// 一次监控任务执行过程的上下文对象。
/// </summary>
/// <remarks>
/// 贯穿执行管道的共享载体，包含任务实体、执行结果、异常信息与步骤间共享数据等。
/// </remarks>
public class PipelineContext
{
    /// <summary>
    /// 当前执行的监控任务实体。
    /// </summary>
    public MonitorEntity Monitor { get; set; }

    /// <summary>
    /// 执行结果（由执行步骤填充，并在断言步骤后追加断言结果）。
    /// </summary>
    public ExecutionResult? Result { get; set; }

    /// <summary>
    /// 执行过程中捕获的异常（如有）。
    /// </summary>
    public Exception? Exception { get; set; }

    /// <summary>
    /// 步骤间共享数据容器。
    /// </summary>
    public Dictionary<string, object> Items { get; } = new();

    /// <summary>
    /// 执行状态（可用于记录运行阶段）。
    /// </summary>
    public TaskStatus Status { get; set; } = TaskStatus.Created;

    /// <summary>
    /// 开始执行时间（UTC）。
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// 取消标记。
    /// </summary>
    public CancellationToken CancellationToken { get; set; } = CancellationToken.None;

    /// <summary>
    /// 创建执行上下文。
    /// </summary>
    /// <param name="monitor">本次要执行的监控任务实体。</param>
    public PipelineContext(MonitorEntity monitor)
    {
        Monitor = monitor ?? throw new ArgumentNullException(nameof(monitor));
    }
}

using AutoTest.Core;

namespace AutoTest.Application.ExecutionPipeline;

public class PipelineContext
{
    // 任务基本信息
    public MonitorEntity Monitor { get; set; }

    // 执行结果
    public ExecutionResult? Result { get; set; }

    // 异常信息
    public Exception? Exception { get; set; }

    // Step 间共享数据
    public Dictionary<string, object> Items { get; } = new();

    // 执行状态
    public TaskStatus Status { get; set; } = TaskStatus.Created;

    // 时间信息
    public DateTime? StartedAt { get; set; }
    // 支持取消
    public CancellationToken CancellationToken { get; set; } = CancellationToken.None;

    public PipelineContext(MonitorEntity monitor)
    {
        Monitor = monitor ?? throw new ArgumentNullException(nameof(monitor));
    }
    // 帮助方法
    public void MarkRunning() => Status = TaskStatus.Running;
    public void MarkSuccess() => Status = TaskStatus.RanToCompletion;
    public void MarkFailed(Exception? ex = null)
    {
        Status = TaskStatus.Faulted;
        Exception = ex;
    }
}

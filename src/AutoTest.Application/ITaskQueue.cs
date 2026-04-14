namespace AutoTest.Application;

/// <summary>
/// 后台任务队列接口。
/// </summary>
/// <remarks>
/// 用于将执行动作排队并由后台消费者按顺序/并发取出执行。
/// </remarks>
public interface ITaskQueue
{
    /// <summary>
    /// 入队一个任务。
    /// </summary>
    ValueTask EnqueueAsync(Func<CancellationToken, Task> task);

    /// <summary>
    /// 出队一个任务（若队列为空则等待）。
    /// </summary>
    ValueTask<Func<CancellationToken, Task>> DequeueAsync(CancellationToken ct);
}

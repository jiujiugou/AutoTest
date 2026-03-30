namespace AutoTest.Application;

public interface ITaskQueue
{
    ValueTask EnqueueAsync(Func<CancellationToken, Task> task);
    ValueTask<Func<CancellationToken, Task>> DequeueAsync(CancellationToken ct);
}

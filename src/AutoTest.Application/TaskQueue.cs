using System.Threading.Channels;

namespace AutoTest.Application;

public class TaskQueue : ITaskQueue
{
    private readonly Channel<Func<CancellationToken, Task>> _queue;

    public TaskQueue()
    {
        _queue = Channel.CreateUnbounded<Func<CancellationToken, Task>>();
    }

    public async ValueTask EnqueueAsync(Func<CancellationToken, Task> task)
    {
        await _queue.Writer.WriteAsync(task);
    }

    public async ValueTask<Func<CancellationToken, Task>> DequeueAsync(CancellationToken ct)
    {
        return await _queue.Reader.ReadAsync(ct);
    }
}

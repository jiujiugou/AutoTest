using AutoTest.Application;
using AutoTest.Core.Execution;
using Microsoft.Extensions.Hosting;

namespace AutoTest.Infrastructure;

public class TaskWorker : BackgroundService
{
    private readonly ITaskQueue _taskQueue;
    private readonly IMonitorService _monitorService;
    private readonly IOrchestrator _orchestrator;
    private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(5); // 控制并发度，假设最多同时执行 5 个任务
    public TaskWorker(ITaskQueue taskQueue, IMonitorService monitorService, IOrchestrator orchestrator)
    {
        _taskQueue = taskQueue;
        _monitorService = monitorService;
        _orchestrator = orchestrator;
    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var pendingTasks = await _monitorService.GetPendingTasksAsync();
        foreach (var task in pendingTasks)
        {
            // 将每个待执行的监控任务加入队列
            await _taskQueue.EnqueueAsync(async ct =>
            {
                try
                {
                    await ExecuteMonitor(task.Id, ct);
                }
                catch (Exception ex)
                {
                    // 这里可以记录日志，或者更新监控状态为失败等
                    Console.WriteLine($"Error executing task {task.Id}: {ex.Message}");
                }
            });
        }
        while (!stoppingToken.IsCancellationRequested)
        {
            var workItem = await _taskQueue.DequeueAsync(stoppingToken);

            _ = Task.Run(async () =>
            {
                await _semaphore.WaitAsync(stoppingToken);
                try
                {
                    await workItem(stoppingToken);
                }
                finally
                {
                    _semaphore.Release();
                }
            });
        }
    }
    private async Task ExecuteMonitor(Guid id, CancellationToken ct)
    {
        try
        {
            var monitor = await _monitorService.GetByIdAsync(id);
            if (monitor == null) return;

            await _orchestrator.TryExecuteAsync(monitor);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error executing task {id}: {ex.Message}");
        }
    }
}

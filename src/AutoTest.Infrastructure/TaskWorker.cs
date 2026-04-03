using AutoTest.Application;
using AutoTest.Core.Execution;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AutoTest.Infrastructure;

public class TaskWorker : BackgroundService
{
    private const int ConsumerCount = 5;
    private static readonly TimeSpan ScheduleInterval = TimeSpan.FromMinutes(5);

    private readonly ITaskQueue _taskQueue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMonitorExecutionCoordinator _executionCoordinator;
    private readonly ILogger<TaskWorker> _logger;

    public TaskWorker(
        ITaskQueue taskQueue,
        IServiceScopeFactory scopeFactory,
        IMonitorExecutionCoordinator executionCoordinator,
        ILogger<TaskWorker> logger)
    {
        _taskQueue = taskQueue;
        _scopeFactory = scopeFactory;
        _executionCoordinator = executionCoordinator;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Scanning for pending monitors...");
        await EnqueueSchedulableMonitorsAsync(stoppingToken);

        using var timer = new PeriodicTimer(ScheduleInterval);
        var periodic = RunPeriodicScheduleAsync(timer, stoppingToken);
        var consumers = Enumerable
            .Range(0, ConsumerCount)
            .Select(_ => ConsumerLoopAsync(stoppingToken))
            .ToArray();

        await Task.WhenAll(consumers.Concat(new[] { periodic }));
    }

    private async Task RunPeriodicScheduleAsync(PeriodicTimer timer, CancellationToken stoppingToken)
    {
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await EnqueueSchedulableMonitorsAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // shutdown
        }
    }

    private async Task ConsumerLoopAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var workItem = await _taskQueue.DequeueAsync(stoppingToken);
                try
                {
                    await workItem(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error executing queued work item");
                }
            }
            catch (OperationCanceledException ex) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Error executing work item from queue");
                break;
            }
        }
    }

    private async Task EnqueueSchedulableMonitorsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var scope = _scopeFactory.CreateScope();
        var monitorService = scope.ServiceProvider.GetRequiredService<IMonitorService>();

        var pendingTasks = await monitorService.GetPendingTasksAsync();
        _logger.LogInformation($"Found {pendingTasks.Count()} pending monitors to enqueue");
        foreach (var task in pendingTasks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var id = task.Id;

            if (!_executionCoordinator.TryBegin(id))
            {
                _logger.LogInformation("Monitor {MonitorId} is already running, skipping", id);
                continue;
            }
            _logger.LogInformation("Enqueuing monitor {MonitorId}", id);
            try
            {
                await _taskQueue.EnqueueAsync(async ct =>
                {
                    try
                    {
                        _logger.LogInformation("Starting execution of monitor {MonitorId}", id);
                        await ExecuteMonitorAsync(id, ct);
                    }
                    finally
                    {
                        _executionCoordinator.End(id);
                        _logger.LogInformation("Finished execution of monitor {MonitorId}", id);
                    }
                });
            }
            catch
            {
                _executionCoordinator.End(id);
                throw;
            }
        }
    }

    private async Task ExecuteMonitorAsync(Guid id, CancellationToken ct)
    {
        _logger.LogInformation("Fetching monitor {MonitorId} from database", id);
        ct.ThrowIfCancellationRequested();

        using var scope = _scopeFactory.CreateScope();
        var monitorService = scope.ServiceProvider.GetRequiredService<IMonitorService>();
        var orchestrator = scope.ServiceProvider.GetRequiredService<IOrchestrator>();

        var monitor = await monitorService.GetByIdAsync(id);
        if (monitor == null)
        {
            _logger.LogWarning("Monitor {MonitorId} not found in database", id);
            return;
        }
        _logger.LogInformation("Executing orchestrator for monitor {MonitorId}", id);
        await orchestrator.TryExecuteAsync(monitor);
        _logger.LogInformation("Executing orchestrator for monitor {MonitorId}", id);
    }
}

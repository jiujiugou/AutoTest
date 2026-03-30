using AutoTest.Application.ExecutionPipeline;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AutoTest.Infrastructure;

public class PipelineBackgroundService : BackgroundService
{
    private readonly IServiceProvider _provider;

    public PipelineBackgroundService(IServiceProvider provider)
    {
        _provider = provider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = _provider.CreateScope();
            var taskRepo = scope.ServiceProvider.GetRequiredService<ITaskRepository>();
            var tasks = await taskRepo.GetPendingTasksAsync();

            foreach (var task in tasks)
            {
                // 标记为 Running
                await taskRepo.UpdateStatusAsync(task.Id, "Running");

                // 执行 Pipeline
                var pipeline = scope.ServiceProvider.GetRequiredService<Pipeline>();
                await pipeline.ExecuteAsync(task);

                // 状态回写数据库
                await taskRepo.UpdateStatusAsync(task.Id, task.Status);
            }

            await Task.Delay(1000, stoppingToken); // 每秒轮询一次
        }
    }
}
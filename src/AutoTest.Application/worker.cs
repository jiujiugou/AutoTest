using System.ComponentModel;
using Microsoft.Extensions.Hosting;

namespace AutoTest.Application;

public class worker : BackgroundService
{
    private readonly ITaskQueue _taskQueue;
    public worker(ITaskQueue taskQueue)
    {
        _taskQueue = taskQueue;
    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var workItem = await _taskQueue.DequeueAsync(stoppingToken);

            try
            {
                await workItem(stoppingToken);
            }
            catch (Exception ex)
            {

            }
        }
    }

}

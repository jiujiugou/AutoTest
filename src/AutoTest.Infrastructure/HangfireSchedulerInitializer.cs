using AutoTest.Application;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace AutoTest.Infrastructure
{
    public class HangfireSchedulerInitializer : IHostedService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<HangfireSchedulerInitializer> _logger;

        public HangfireSchedulerInitializer(IServiceProvider serviceProvider, ILogger<HangfireSchedulerInitializer> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Initializing Hangfire scheduled jobs...");

            using var scope = _serviceProvider.CreateScope();
            var scheduler = scope.ServiceProvider.GetRequiredService<IWorkflowScheduler>();

            // 注册到 Hangfire
            await scheduler.ScheduleAsync("ScanAndEnqueue", "Daily");
            

            _logger.LogInformation("Hangfire scheduled jobs initialized.");
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}

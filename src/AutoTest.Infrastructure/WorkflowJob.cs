using AutoTest.Application;
using AutoTest.Core.Abstraction;
using AutoTest.Core.Execution;
using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace AutoTest.Infrastructure
{
    internal class WorkflowJob
    {
        private readonly RedisLockService _lockService;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<WorkflowJob> _logger;
        public WorkflowJob( IServiceProvider serviceProvider, ILogger<WorkflowJob> logger, RedisLockService lockService)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _lockService = lockService;
        }

        public async Task RunAsync(Guid monitorId)
        {
            var redisKey = $"monitor-lock:{monitorId}";

            var lockService = new RedisLockService("localhost:6379");
            using var myLock = lockService.GetLock($"{redisKey}", TimeSpan.FromSeconds(10));
            _logger.LogInformation("WorkflowJob started for monitor: {Id}", monitorId);
            if (!await myLock.AcquireAsync())
            {
                _logger.LogInformation("Monitor {Id} is already being processed by another instance.", monitorId);
                return;
            }
            try
            {
               
                using var scope = _serviceProvider.CreateScope();
                var monitorRepository = scope.ServiceProvider.GetRequiredService<IMonitorRepository>();
                var orchestrator = scope.ServiceProvider.GetRequiredService<IOrchestrator>();

                var monitor = await monitorRepository.GetByIdAsync(monitorId);
                if (monitor == null)
                {
                    _logger.LogWarning("Monitor not found in TaskRunAsync: {Id}", monitorId);
                    return;
                }
                _logger.LogInformation("Executing monitor: {Id}", monitorId);
                await orchestrator.TryExecuteAsync(monitor);
             }
             catch (Exception ex)
             {
                 _logger.LogError(ex, "Error executing monitor: {Id}", monitorId);
             }
            finally
            {
                // 异步释放锁
                await myLock.ReleaseAsync();
                _logger.LogInformation("WorkflowJob completed for monitor: {Id}", monitorId);
            }
        }

        [DisableConcurrentExecution(timeoutInSeconds: 60)]
        public async Task ScanAndEnqueueAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var monitorService = scope.ServiceProvider.GetRequiredService<IMonitorService>();
            var pendingTasks = await monitorService.GetPendingTasksAsync();
            _logger.LogInformation($"Found {pendingTasks.Count()} pending monitors");

            foreach (var task in pendingTasks)
            {
                var monitorId = task.Id;
                BackgroundJob.Enqueue<WorkflowJob>(job => job.RunAsync(monitorId));
            }
        }
    }
}

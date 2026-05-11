using AutoTest.Application;
using AutoTest.Core;
using AutoTest.Core.Abstraction;
using AutoTest.Core.Execution;
using Hangfire;
using LockCommons;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using AutoTest.Infrastructure.Hubs;

namespace AutoTest.Infrastructure
{
    internal class WorkflowJob
    {
        private readonly RedisLockService _redisLockService;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<WorkflowJob> _logger;
        private readonly IHubContext<MonitorHub> _hub;

        public WorkflowJob(IServiceProvider serviceProvider, ILogger<WorkflowJob> logger, RedisLockService redisLockService, IHubContext<MonitorHub> hub)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _redisLockService = redisLockService;
            _hub = hub;
        }

        public async Task RunAsync(Guid monitorId)
        {
            var key = $"schedule:{monitorId}:{DateTime.UtcNow:yyyyMMdd}";
            await RunAsync(monitorId, null, key);
        }

        public async Task RunAsync(Guid monitorId, string? userId)
        {
            await RunAsync(monitorId, userId, null);
        }

        public async Task RunAsync(Guid monitorId, string? userId, string? idempotencyKey)
        {
            var redisKey = $"monitor-lock:{monitorId}";

            await using var myLock = _redisLockService.CreateLock($"{redisKey}", TimeSpan.FromSeconds(10));
            if (!await myLock.AcquireAsync())
            {
                _logger.LogInformation("Monitor {Id} is already being processed by another instance.", monitorId);
                return;
            }
            _logger.LogInformation("WorkflowJob started for monitor: {Id}", monitorId);
            try
            {
               
                using var scope = _serviceProvider.CreateScope();
                var monitorRepository = scope.ServiceProvider.GetRequiredService<IMonitorRepository>();
                var orchestrator = scope.ServiceProvider.GetRequiredService<IOrchestrator>();
                var monitorService = scope.ServiceProvider.GetRequiredService<IMonitorService>();
                var executionRecordRepository = scope.ServiceProvider.GetRequiredService<IExecutionRecordRepository>();
                var workflowScheduler = scope.ServiceProvider.GetRequiredService<IWorkflowScheduler>();
                var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

                var monitor = await monitorRepository.GetByIdAsync(monitorId);
                if (monitor == null)
                {
                    _logger.LogWarning("Monitor not found in TaskRunAsync: {Id}", monitorId);
                    return;
                }
                _logger.LogInformation("Executing monitor: {Id}", monitorId);
                var lockedBy = $"{Environment.MachineName}:{Environment.ProcessId}:{Guid.NewGuid():N}";
                var start = await monitorService.TryStartExecutionAsync(monitorId, idempotencyKey, lockedBy);
                if (!start.Started)
                {
                    _logger.LogInformation("Duplicate execution detected for monitor {Id}, skipping.", monitorId);
                    return;
                }

                // 同步本地对象状态：TryStartExecutionAsync 已在 DB 中将 Monitor 标为 Running
                monitor.MarkRunning();

                await PublishAsync(userId, new { monitorId, status = "running", executionId = start.ExecutionId });

                try
                {
                    using var heartbeatCts = new CancellationTokenSource();
                    var heartbeatTask = Task.Run(async () =>
                    {
                        while (!heartbeatCts.IsCancellationRequested)
                        {
                            try
                            {
                                await executionRecordRepository.UpdateHeartbeatAsync(
                                    start.ExecutionId,
                                    lockedBy,
                                    DateTime.UtcNow,
                                    heartbeatCts.Token);
                            }
                            catch
                            {
                            }

                            try
                            {
                                await Task.Delay(TimeSpan.FromSeconds(5), heartbeatCts.Token);
                            }
                            catch
                            {
                                break;
                            }
                        }
                    }, heartbeatCts.Token);

                    try
                    {
                        await orchestrator.TryExecuteAsync(monitor, start.ExecutionId, start.StartedAtUtc, lockedBy);
                    }
                    finally
                    {
                        heartbeatCts.Cancel();
                        try { await heartbeatTask; } catch { }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error executing monitor: {Id}", monitorId);
                    // 兜底：确保 Monitor 状态不会卡在 Running
                    try
                    {
                        monitor.ForceFailed();
                        await unitOfWork.ExecuteAsync(async tx =>
                        {
                            await monitorRepository.UpdateAsync(monitor, tx);
                            await executionRecordRepository.UpdateCompletionAsync(
                                start.ExecutionId,
                                (int)MonitorStatus.Failed,
                                DateTime.UtcNow,
                                false,
                                ex.Message,
                                "Exception",
                                "{\"reason\":\"orchestrator_failure\"}",
                                tx);
                        });
                    }
                    catch (Exception fallbackEx)
                    {
                        _logger.LogError(fallbackEx, "Failed to mark monitor {Id} as Failed after orchestrator error", monitorId);
                    }
                }
                finally
                {
                    try
                    {
                        var record = await monitorService.GetLatestExecutionAsync(monitorId);
                        if (record != null)
                        {
                            var assertions = await monitorService.GetExecutionAssertionResultsAsync(record.Id);
                            await PublishAsync(userId, new
                            {
                                monitorId,
                                status = "finished",
                                executionId = start.ExecutionId,
                                record,
                                assertions
                            });
                        }
                        else
                        {
                            await PublishAsync(userId, new { monitorId, status = "finished", record = (object?)null, assertions = Array.Empty<object>() });
                        }

                        if (string.IsNullOrWhiteSpace(userId))
                        {
                            var shouldDisable = await monitorService.IncrementAutoExecutedCountAndDisableIfReachedAsync(monitorId);
                            if (shouldDisable)
                                await workflowScheduler.RemoveMonitorScheduleAsync(monitorId);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to publish monitor update: {Id}", monitorId);
                    }
                }
             }
             catch (Exception ex)
             {
                 _logger.LogError(ex, "Error executing monitor: {Id}", monitorId);
             }
            finally
            {
                try
                {
                    await myLock.ReleaseAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to release lock for monitor: {Id}", monitorId);
                }
                _logger.LogInformation("WorkflowJob completed for monitor: {Id}", monitorId);
            }
        }

        /// <summary>
        /// 向前端推送监控执行状态更新。
        /// </summary>
        /// <param name="userId">目标用户；为空则推送到管理员组。</param>
        /// <param name="payload">推送载荷。</param>
        private Task PublishAsync(string? userId, object payload)
        {
            if (!string.IsNullOrWhiteSpace(userId))
                return _hub.Clients.Group(MonitorHub.GroupNames.User(userId)).SendAsync("monitorUpdated", payload);

            return _hub.Clients.Group(MonitorHub.GroupNames.Role("admin")).SendAsync("monitorUpdated", payload);
        }
    }
}

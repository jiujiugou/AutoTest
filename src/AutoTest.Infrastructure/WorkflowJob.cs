using AutoTest.Application;
using AutoTest.Core.Abstraction;
using AutoTest.Core.Execution;
using Hangfire;
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
    /// <summary>
    /// Hangfire 任务执行入口：负责获取分布式锁/幂等标记，加载监控实体并触发编排执行，同时通过 SignalR 推送运行状态。
    /// </summary>
    internal class WorkflowJob
    {
        private readonly RedisService _redisService;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<WorkflowJob> _logger;
        private readonly IHubContext<MonitorHub> _hub;

        /// <summary>
        /// 初始化 <see cref="WorkflowJob"/>。
        /// </summary>
        /// <param name="serviceProvider">依赖注入根容器，用于创建执行作用域。</param>
        /// <param name="logger">日志记录器。</param>
        /// <param name="redisService">Redis 服务，用于分布式锁与幂等标记。</param>
        /// <param name="hub">监控 SignalR Hub 上下文。</param>
        public WorkflowJob(IServiceProvider serviceProvider, ILogger<WorkflowJob> logger, RedisService redisService, IHubContext<MonitorHub> hub)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _redisService = redisService;
            _hub = hub;
        }

        /// <summary>
        /// 执行一次监控任务（无用户上下文，通知广播给管理员组）。
        /// </summary>
        /// <param name="monitorId">监控任务 ID。</param>
        public async Task RunAsync(Guid monitorId)
        {
            var key = $"schedule:{monitorId}:{DateTime.UtcNow:yyyyMMdd}";
            await RunAsync(monitorId, null, key);
        }

        /// <summary>
        /// 执行一次监控任务（可选用户上下文，通知可定向推送给触发用户）。
        /// </summary>
        /// <param name="monitorId">监控任务 ID。</param>
        /// <param name="userId">触发用户 ID；为空时视为系统触发。</param>
        public async Task RunAsync(Guid monitorId, string? userId)
        {
            await RunAsync(monitorId, userId, null);
        }

        public async Task RunAsync(Guid monitorId, string? userId, string? idempotencyKey)
        {
            var redisKey = $"monitor-lock:{monitorId}";

            await using var myLock = _redisService.GetLock($"{redisKey}", TimeSpan.FromSeconds(10));
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

                monitor = await monitorRepository.GetByIdAsync(monitorId);
                if (monitor == null)
                    return;

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

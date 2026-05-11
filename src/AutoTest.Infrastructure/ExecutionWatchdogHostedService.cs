using System.Data;
using System.Text.Json;
using AutoTest.Application;
using AutoTest.Core;
using AutoTest.Core.Outbox;
using AutoTest.Core.Abstraction;
using AutoTest.Infrastructure.Hubs;
using Dapper;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AutoTest.Infrastructure;

public sealed class ExecutionWatchdogHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ExecutionWatchdogHostedService> _logger;

    public ExecutionWatchdogHostedService(IServiceScopeFactory scopeFactory, ILogger<ExecutionWatchdogHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await TickAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Execution watchdog tick failed");
            }

            try
            {
                await timer.WaitForNextTickAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task TickAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var conn = scope.ServiceProvider.GetRequiredService<IDbConnection>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var execRepo = scope.ServiceProvider.GetRequiredService<IExecutionRecordRepository>();
        var outboxRepo = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();
        var hub = scope.ServiceProvider.GetRequiredService<IHubContext<MonitorHub>>();

        var now = DateTime.UtcNow;
        var staleHeartbeatAtUtc = now.AddMinutes(-2);
        var staleStartedAtUtc = now.AddMinutes(-2);

        const string selectSql = """
            SELECT TOP (50)
                r.Id AS ExecutionId,
                r.MonitorId,
                r.StartedAt,
                m.Name AS MonitorName,
                m.TargetType,
                m.TargetConfig
            FROM ExecutionRecord r
            INNER JOIN Monitor m ON m.Id = r.MonitorId
            WHERE r.Status = @Running
              AND r.FinishedAt IS NULL
              AND (
                   (r.HeartbeatAtUtc IS NOT NULL AND r.HeartbeatAtUtc <= @StaleHeartbeatAtUtc)
                OR (r.HeartbeatAtUtc IS NULL     AND r.StartedAt      <= @StaleStartedAtUtc)
              )
            ORDER BY r.StartedAt ASC
            """;

        var candidates = (await conn.QueryAsync<StaleExecutionRow>(selectSql, new
        {
            Running = (int)MonitorStatus.Running,
            StaleHeartbeatAtUtc = staleHeartbeatAtUtc,
            StaleStartedAtUtc = staleStartedAtUtc
        })).ToList();

        if (candidates.Count == 0)
            return;

        foreach (var row in candidates)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            try
            {
                var finishedAt = DateTime.UtcNow;

                await uow.ExecuteAsync(async tx =>
                {
                    // 1. 更新执行记录
                    await execRepo.UpdateCompletionAsync(
                        row.ExecutionId,
                        (int)MonitorStatus.Timeout,
                        finishedAt,
                        false,
                        "Execution watchdog timeout: no heartbeat for 2 minutes",
                        "Timeout",
                        "{\"reason\":\"stale\"}",
                        tx);

                    // 2. 更新 Monitor 状态（仅当仍是 Running 时）
                    await conn.ExecuteAsync("""
                        UPDATE Monitor
                        SET Status = @Timeout, LastRunTime = @Now
                        WHERE Id = @Id AND Status = @Running
                        """,
                        new
                        {
                            Id = row.MonitorId,
                            Timeout = (int)MonitorStatus.Timeout,
                            Running = (int)MonitorStatus.Running,
                            Now = finishedAt
                        }, tx);

                    // 3. 写入 OutboxMessage，触发 AI 分析
                    var payload = new MonitorExecutionFailedPayload
                    {
                        MonitorId = row.MonitorId,
                        MonitorName = row.MonitorName,
                        ExecutionId = row.ExecutionId,
                        StartedAt = row.StartedAt,
                        FinishedAt = finishedAt,
                        FailureType = FailureType.Timeout,
                        IsExecutionSuccess = false,
                        IsAssertionSuccess = false,
                        ErrorMessage = "Execution watchdog timeout: no heartbeat for 2 minutes",
                        TargetType = row.TargetType,
                        TargetConfig = row.TargetConfig
                    };

                    var outbox = new OutboxMessage
                    {
                        Id = Guid.NewGuid(),
                        Type = "monitor.execution.failed",
                        PayloadJson = JsonSerializer.Serialize(payload),
                        OccurredAt = finishedAt,
                        Status = OutboxStatus.Pending,
                        Attempts = 0
                    };

                    await outboxRepo.AddAsync(outbox, tx);
                });

                // 4. SignalR 推送（事务外，推送失败不影响状态更新）
                try
                {
                    await hub.Clients
                        .Group(MonitorHub.GroupNames.Role("admin"))
                        .SendAsync("monitorUpdated", new
                        {
                            monitorId = row.MonitorId,
                            status = "timeout",
                            executionId = row.ExecutionId
                        });
                }
                catch (Exception pushEx)
                {
                    _logger.LogWarning(pushEx, "Failed to push watchdog notification for {ExecutionId}", row.ExecutionId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to mark stale execution {ExecutionId} as timeout", row.ExecutionId);
            }
        }
    }

    private sealed record StaleExecutionRow(
        Guid ExecutionId,
        Guid MonitorId,
        DateTime StartedAt,
        string MonitorName,
        string? TargetType,
        string? TargetConfig);
}

using System.Data;
using AutoTest.Application;
using AutoTest.Core;
using AutoTest.Core.Abstraction;
using Dapper;
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

        var now = DateTime.UtcNow;
        var staleHeartbeatAtUtc = now.AddMinutes(-2);
        var staleStartedAtUtc = now.AddMinutes(-15);

        var isSqlServer = conn is Microsoft.Data.SqlClient.SqlConnection;
        var selectSql = isSqlServer
            ? """
              SELECT TOP (50)
                  Id,
                  MonitorId
              FROM ExecutionRecord
              WHERE Status = @Running
                AND FinishedAt IS NULL
                AND (
                     (HeartbeatAtUtc IS NOT NULL AND HeartbeatAtUtc <= @StaleHeartbeatAtUtc)
                  OR (HeartbeatAtUtc IS NULL AND StartedAt <= @StaleStartedAtUtc)
                )
              ORDER BY StartedAt ASC
              """
            : """
              SELECT
                  Id,
                  MonitorId
              FROM ExecutionRecord
              WHERE Status = @Running
                AND FinishedAt IS NULL
                AND (
                     (HeartbeatAtUtc IS NOT NULL AND HeartbeatAtUtc <= @StaleHeartbeatAtUtc)
                  OR (HeartbeatAtUtc IS NULL AND StartedAt <= @StaleStartedAtUtc)
                )
              ORDER BY StartedAt ASC
              LIMIT 50
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
                await uow.ExecuteAsync(async tx =>
                {
                    await execRepo.UpdateCompletionAsync(
                        row.Id,
                        (int)MonitorStatus.Timeout,
                        now,
                        false,
                        "Execution watchdog timeout",
                        "Timeout",
                        "{\"reason\":\"stale\"}",
                        tx);

                    await conn.ExecuteAsync(
                        """
                        UPDATE Monitor
                        SET Status = @Timeout,
                            LastRunTime = @Now
                        WHERE Id = @Id
                          AND Status = @Running
                        """,
                        new
                        {
                            Id = row.MonitorId,
                            Timeout = (int)MonitorStatus.Timeout,
                            Running = (int)MonitorStatus.Running,
                            Now = now
                        },
                        tx);
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to mark stale execution {ExecutionId} as timeout", row.Id);
            }
        }
    }

    private sealed record StaleExecutionRow(Guid Id, Guid MonitorId);
}

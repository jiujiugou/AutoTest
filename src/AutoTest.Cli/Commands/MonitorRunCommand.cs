using System.Text.Json;
using AutoTest.Application;
using Microsoft.Extensions.DependencyInjection;

namespace AutoTest.Cli.Commands;

/// <summary>
/// autotest monitor run 命令：按 ID 执行一个已持久化的监控任务。
/// </summary>
public static class MonitorRunCommand
{
    public static async Task<int> ExecuteAsync(string[] args, IServiceProvider services)
    {
        var idStr = args.FirstOrDefault(a => !a.StartsWith('-'));
        if (string.IsNullOrWhiteSpace(idStr) || !Guid.TryParse(idStr, out var monitorId))
        {
            Console.Error.WriteLine("Usage: autotest monitor run <id> [--json]");
            return 2;
        }

        bool jsonOutput = args.Contains("--json");

        using var scope = services.CreateScope();
        var monitorService = scope.ServiceProvider.GetRequiredService<IMonitorService>();
        var orchestrator = scope.ServiceProvider.GetRequiredService<AutoTest.Core.Execution.IOrchestrator>();

        var idempotencyKey = $"cli:{monitorId:N}:{DateTime.UtcNow:yyyyMMddHHmmss}";

        try
        {
            var (started, executionId, startedAtUtc) =
                await monitorService.TryStartExecutionAsync(monitorId, idempotencyKey, "cli");

            if (!started)
            {
                Console.Error.WriteLine($"Monitor {monitorId} is already running or idempotent key conflict.");
                return 2;
            }

            var monitor = await monitorService.GetByIdAsync(monitorId);
            if (monitor == null)
            {
                Console.Error.WriteLine($"Monitor not found: {monitorId}");
                return 2;
            }

            var result = await orchestrator.TryExecuteAsync(monitor, executionId, startedAtUtc, "cli");

            if (jsonOutput)
            {
                Console.WriteLine(JsonSerializer.Serialize(new
                {
                    success = result.IsExecutionSuccess,
                    errorMessage = result.ErrorMessage,
                    assertions = result.Assertions.Select(a => new { a.Target, a.IsSuccess, a.Actual, a.Expected, a.Message })
                }, new JsonSerializerOptions { WriteIndented = true }));
            }
            else
            {
                var pass = result.IsExecutionSuccess && result.Assertions.All(a => a.IsSuccess);
                Console.WriteLine(pass ? "PASS" : "FAIL");
                if (!string.IsNullOrEmpty(result.ErrorMessage))
                    Console.WriteLine($"Error: {result.ErrorMessage}");
                foreach (var a in result.Assertions)
                    Console.WriteLine($"  [{a.Target}] {(a.IsSuccess ? "OK" : "FAIL")} expected={a.Expected} actual={a.Actual}");
            }

            var pass2 = result.IsExecutionSuccess && result.Assertions.All(a => a.IsSuccess);
            return pass2 ? 0 : 1;
        }
        catch (Exception ex)
        {
            if (jsonOutput)
                Console.WriteLine(JsonSerializer.Serialize(new { success = false, error = ex.Message }));
            else
                Console.Error.WriteLine($"Error: {ex.Message}");
            return 2;
        }
    }
}

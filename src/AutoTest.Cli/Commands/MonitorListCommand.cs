using System.Text.Json;
using AutoTest.Application;
using Microsoft.Extensions.DependencyInjection;

namespace AutoTest.Cli.Commands;

/// <summary>
/// autotest monitor list 命令：列出 DB 中的监控任务。
/// </summary>
public static class MonitorListCommand
{
    public static async Task<int> ExecuteAsync(string[] args, IServiceProvider services)
    {
        bool jsonOutput = args.Contains("--json");
        int take = 50;
        var takeIdx = Array.IndexOf(args, "--take");
        if (takeIdx >= 0 && takeIdx + 1 < args.Length)
            int.TryParse(args[takeIdx + 1], out take);

        using var scope = services.CreateScope();
        var monitorService = scope.ServiceProvider.GetRequiredService<IMonitorService>();

        var monitors = await monitorService.ListAsync(take);

        if (jsonOutput)
        {
            var items = monitors.Select(m => new
            {
                m.Id,
                m.Name,
                TargetType = m.Target.Type,
                Status = (int)m.Status,
                m.IsEnabled,
                m.AutoDailyEnabled,
                AssertionCount = m.Assertions.Count
            });
            Console.WriteLine(JsonSerializer.Serialize(items, new JsonSerializerOptions { WriteIndented = true }));
        }
        else
        {
            Console.WriteLine($"{"ID",-38} {"Name",-30} {"Type",-10} Status");
            Console.WriteLine(new string('-', 90));
            foreach (var m in monitors)
                Console.WriteLine($"{m.Id,-38} {m.Name[..Math.Min(m.Name.Length, 28)],-30} {m.Target.Type,-10} {m.Status}");
            Console.WriteLine($"\n{monitors.Count()} monitors");
        }

        return 0;
    }
}

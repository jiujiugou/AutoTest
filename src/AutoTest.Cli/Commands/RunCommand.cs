using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace AutoTest.Cli.Commands;

/// <summary>
/// autotest run 命令：直接执行 DSL JSON 文件，不依赖 DB。
/// </summary>
public static class RunCommand
{
    public static async Task<int> ExecuteAsync(string[] args, IServiceProvider services)
    {
        var filePath = args.FirstOrDefault(a => !a.StartsWith('-'));
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            Console.Error.WriteLine("Usage: autotest run <dsl-file> [--var key=value ...] [--json] [--timeout seconds]");
            return 2;
        }

        var variables = new Dictionary<string, string>();
        bool jsonOutput = false;
        int timeout = 60;

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--var" && i + 1 < args.Length)
            {
                var parts = args[++i].Split('=', 2);
                if (parts.Length == 2)
                    variables[parts[0]] = parts[1];
            }
            else if (args[i] == "--json")
                jsonOutput = true;
            else if (args[i] == "--timeout" && i + 1 < args.Length)
                int.TryParse(args[++i], out timeout);
        }

        using var scope = services.CreateScope();
        var runner = scope.ServiceProvider.GetRequiredService<DslRunner>();

        try
        {
            var result = await runner.RunAsync(filePath, variables, timeout);

            if (jsonOutput)
            {
                var output = new
                {
                    success = result.Success,
                    errorMessage = result.ErrorMessage,
                    assertions = result.Assertions.Select(a => new { a.Target, a.IsSuccess, a.Actual, a.Expected, a.Message }),
                    steps = result.DslExecutionResult?.Steps.Select(s => new { s.StepName, s.Type, s.IsSuccess, s.StatusCode, s.ElapsedMs, s.ErrorMessage })
                };
                Console.WriteLine(JsonSerializer.Serialize(output, new JsonSerializerOptions { WriteIndented = true }));
            }
            else
            {
                Console.WriteLine(result.Success ? "PASS" : "FAIL");
                if (!string.IsNullOrEmpty(result.ErrorMessage))
                    Console.WriteLine($"Error: {result.ErrorMessage}");

                if (result.DslExecutionResult?.Steps is { Count: > 0 } steps)
                {
                    Console.WriteLine($"\nSteps ({steps.Count}):");
                    foreach (var s in steps)
                        Console.WriteLine($"  [{s.StepName}] {s.Type} {(s.IsSuccess ? "OK" : "FAIL")} {s.ElapsedMs}ms {(s.ErrorMessage != null ? "- " + s.ErrorMessage : "")}");
                }
            }

            return result.Success ? 0 : 1;
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

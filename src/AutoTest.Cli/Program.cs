using AutoTest.Cli;
using AutoTest.Cli.Commands;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

// Load configuration
var configPath = args.FirstOrDefault(a => a.StartsWith("--config="))?[9..]
    ?? "appsettings.json";

if (!File.Exists(configPath))
    configPath = FindConfigFile();

var configuration = new ConfigurationBuilder()
    .AddJsonFile(configPath, optional: true)
    .AddEnvironmentVariables()
    .Build();

services.AddSingleton<IConfiguration>(configuration);
services.AddAutoTestCli(configuration);

var provider = services.BuildServiceProvider();

if (args.Length == 0)
{
    Console.WriteLine("AutoTest CLI");
    Console.WriteLine("  autotest run <dsl-file> [--var k=v] [--json] [--timeout seconds]");
    Console.WriteLine("  autotest monitor run <id> [--json]");
    Console.WriteLine("  autotest monitor list [--json] [--take 50]");
    Console.WriteLine("  autotest --config=<path>  (default: appsettings.json)");
    return 0;
}

var command = args[0].ToLowerInvariant();
var remainingArgs = args[1..];

int exitCode = command switch
{
    "run" => await RunCommand.ExecuteAsync(remainingArgs, provider),
    "monitor" => remainingArgs.Length > 0 && remainingArgs[0].ToLowerInvariant() == "run"
        ? await MonitorRunCommand.ExecuteAsync(remainingArgs[1..], provider)
        : remainingArgs.Length > 0 && remainingArgs[0].ToLowerInvariant() == "list"
            ? await MonitorListCommand.ExecuteAsync(remainingArgs[1..], provider)
            : PrintMonitorUsage(),
    _ => PrintUsage()
};

return exitCode;

static int PrintUsage()
{
    Console.Error.WriteLine("Unknown command. Use: autotest run | monitor");
    return 2;
}

static int PrintMonitorUsage()
{
    Console.Error.WriteLine("Usage: autotest monitor run <id> | autotest monitor list");
    return 2;
}

static string FindConfigFile()
{
    var dir = AppContext.BaseDirectory;
    for (int i = 0; i < 5; i++)
    {
        var path = Path.Combine(dir, "appsettings.json");
        if (File.Exists(path)) return path;
        var parent = Path.GetDirectoryName(dir);
        if (parent == dir) break;
        dir = parent;
    }
    return "appsettings.json";
}

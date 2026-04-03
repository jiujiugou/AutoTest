using Serilog;
using Serilog.Sinks.File;

namespace AutoTest.Webapi;

public class LoggingOptions
{
    public string FilePath { get; set; } = "logs/log-.txt";

    public Serilog.Events.LogEventLevel MinimumLevel { get; set; }
        = Serilog.Events.LogEventLevel.Information;

    public bool EnableConsole { get; set; } = true;

    public bool EnableFile { get; set; } = true;
}
public static class LoggingServiceExtensions
{
    public static IServiceCollection AddMyLogging(
        this IServiceCollection services,
        Action<LoggingOptions>? configure = null)
    {
        var options = new LoggingOptions();
        configure?.Invoke(options);

        var loggerConfig = new LoggerConfiguration()
            .MinimumLevel.Is(options.MinimumLevel)
            .Enrich.FromLogContext();

        if (options.EnableConsole)
        {
            loggerConfig = loggerConfig.WriteTo.Console();
        }

        if (options.EnableFile)
        {
            loggerConfig = loggerConfig.WriteTo.File(
                options.FilePath,
                rollingInterval: RollingInterval.Day,
                outputTemplate:
                "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}"
            );
        }

        Log.Logger = loggerConfig.CreateLogger();

        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddSerilog();
        });

        return services;
    }
}
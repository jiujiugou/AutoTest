using Serilog;
using Serilog.Sinks.File;

namespace AutoTest.Webapi;

public class LoggingOptions
{
    public string FilePath { get; set; } = "logs/log-.txt"; // 默认路径
    public Serilog.Events.LogEventLevel MinimumLevel { get; set; } = Serilog.Events.LogEventLevel.Information;
}
public static class LoggingServiceExtensions
{
    public static IServiceCollection AddMyLogging(this IServiceCollection services, LoggingOptions options)
    {
        // 配置 Serilog
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Is(options.MinimumLevel)
            .WriteTo.File(
                options.FilePath,
                rollingInterval: RollingInterval.Day,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}"
            )
            .CreateLogger();
        services.AddLogging(builder =>
        {
            builder.ClearProviders(); // 清掉默认日志
            builder.AddSerilog();
        });
        return services;
    }
}

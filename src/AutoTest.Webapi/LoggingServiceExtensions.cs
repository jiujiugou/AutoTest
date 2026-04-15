using Serilog;
using Serilog.Sinks.Elasticsearch;

namespace AutoTest.Webapi;

public class LoggingOptions
{
    public string ElasticNodes { get; set; } = "http://localhost:9200";

    public Serilog.Events.LogEventLevel MinimumLevel { get; set; }
        = Serilog.Events.LogEventLevel.Information;

    public bool EnableConsole { get; set; } = true;
    public bool EnableElasticsearch { get; set; } = true;
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
            .Enrich.FromLogContext()
            .WriteTo.Sink(new SignalRLogSink());

        if (options.EnableConsole)
        {
            loggerConfig.WriteTo.Console();
        }

        if (options.EnableElasticsearch)
        {
            var nodes = options.ElasticNodes.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(n => new Uri(n.Trim()));

            loggerConfig.WriteTo.Elasticsearch(new ElasticsearchSinkOptions(nodes)
            {
                AutoRegisterTemplate = true,
                AutoRegisterTemplateVersion = AutoRegisterTemplateVersion.ESv8,
                IndexFormat = "autotest-logs-{0:yyyy.MM.dd}",
                BatchAction = ElasticOpType.Index
            });
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
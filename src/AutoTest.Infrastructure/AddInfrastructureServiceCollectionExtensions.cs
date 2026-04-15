using AutoTest.Application;
using AutoTest.Application.Builder;
using AutoTest.Core.Abstraction;
using Dapper;
using Hangfire;
using Hangfire.SQLite;
using Hangfire.SqlServer;
using AutoTest.Infrastructure.Outbox;
using AutoTest.Infrastructure.Mapper.AssertionMapper;
using AutoTest.Infrastructure.Mapper.AssertionRuleMapper;
using AutoTest.Infrastructure.Mapper.TargetMapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Data;

namespace AutoTest.Infrastructure;

/// <summary>
/// Infrastructure 层依赖注入注册入口：负责将仓储、调度、HostedService、映射器等基础设施实现注册到容器。
/// </summary>
public static class AddInfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// 注册 AutoTest 的 Infrastructure 层实现。
    /// </summary>
    /// <param name="services">服务集合。</param>
    /// <param name="configuration">应用配置。</param>
    /// <returns>同一个 <see cref="IServiceCollection"/>，用于链式调用。</returns>
    public static IServiceCollection AddAutoTestInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        DapperTypeHandlers.EnsureInitialized();
        var provider = configuration["Database:Provider"] ?? "SqlServer";
        var hangfireConnection = configuration.GetConnectionString("HangfireConnection")
                                 ?? configuration["ConnectionStrings:HangfireConnection"]
                                 ?? throw new InvalidOperationException("Missing connection string: HangfireConnection");
        hangfireConnection = hangfireConnection.Trim();
        if (!hangfireConnection.EndsWith(";", StringComparison.Ordinal))
            hangfireConnection += ";";

        services.AddSingleton<IWorkflowScheduler, HangfireWorkflowScheduler>();
        services.AddHangfire(config =>
        {
            config.UseSimpleAssemblyNameTypeSerializer()
                  .UseRecommendedSerializerSettings();

            if (string.Equals(provider, "Sqlite", StringComparison.OrdinalIgnoreCase))
            {
                config.UseSQLiteStorage(hangfireConnection);
            }
            else
            {
                config.UseSqlServerStorage(hangfireConnection, new SqlServerStorageOptions
                {
                    QueuePollInterval = TimeSpan.FromSeconds(1),
                    PrepareSchemaIfNecessary = true,
                    UseRecommendedIsolationLevel = true,
                    DisableGlobalLocks = true
                });
            }
        });
        services.AddHangfireServer(options =>
        {
            options.WorkerCount = 5;         
            options.SchedulePollingInterval = TimeSpan.FromSeconds(5);
        });
        services.AddTransient<WorkflowJob>();
        services.AddScoped<IMonitorRepository, MonitorRepository>();
        services.AddScoped<IExecutionRecordRepository, ExecutionRecordRepository>();
        services.AddScoped<IOutboxRepository, DapperOutboxRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<ILogService, LogService>();
        services.AddScoped<ITargetMap, HttpTargetMap>();
        services.AddScoped<ITargetMap, TcpTargetMap>();
        services.AddScoped<ITargetMap, PythonTargetMap>();
        services.AddScoped<IAssertionMap, HttpAssertionMap>();
        services.AddScoped<IAssertionMap, TcpAssertionMap>();
        services.AddScoped<IAssertionMap, DbAssertionMap>();
        services.AddScoped<IAssertionMap, PythonAssertionMap>();
        services.AddScoped<IAssertionRuleMap, HttpAssertionRuleMap>();
        services.AddScoped<IAssertionRuleMap, TcpAssertionRuleMap>();
        services.AddScoped<IAssertionRuleMap, DbAssertionRuleMap>();
        services.AddScoped<IAssertionRuleMap, PythonAssertionRuleMap>();
        
        var elasticNodes = configuration["Logging:ElasticNodes"] ?? "http://localhost:9200";
        var firstNode = elasticNodes.Split(',', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "http://localhost:9200";
        services.AddSingleton(new Elastic.Clients.Elasticsearch.ElasticsearchClient(new Uri(firstNode)));
        
        services.AddHostedService<ExecutionWatchdogHostedService>();
        services.Configure<OutboxWebhookOptions>(configuration.GetSection("Outbox:Webhook"));
        services.AddHttpClient(OutboxWebhookDispatcherHostedService.HttpClientName);
        services.AddHostedService<OutboxWebhookDispatcherHostedService>();
        services.AddSingleton<RedisService>(sp =>
        {
            var redisConnection = "localhost:6379";
            return new RedisService(redisConnection);
        });
        return services;
    }
}

/// <summary>
/// Dapper 类型处理器注册器，用于统一数据库字段与 .NET 类型之间的转换行为。
/// </summary>
internal static class DapperTypeHandlers
{
    private static bool _initialized;

    /// <summary>
    /// 确保 Dapper 类型处理器只被注册一次。
    /// </summary>
    public static void EnsureInitialized()
    {
        if (_initialized)
            return;

        SqlMapper.AddTypeHandler(new GuidTypeHandler());
        _initialized = true;
    }

    private sealed class GuidTypeHandler : SqlMapper.TypeHandler<Guid>
    {
        /// <inheritdoc />
        public override void SetValue(IDbDataParameter parameter, Guid value)
        {
            parameter.DbType = DbType.Guid;
            parameter.Value = value;
        }

        /// <inheritdoc />
        public override Guid Parse(object value)
        {
            return value switch
            {
                Guid g => g,
                string s => Guid.Parse(s),
                byte[] b => new Guid(b),
                _ => Guid.Parse(value.ToString()!)
            };
        }
    }
}

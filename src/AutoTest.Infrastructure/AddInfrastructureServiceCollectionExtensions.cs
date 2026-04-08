using AutoTest.Application;
using AutoTest.Core.Abstraction;
using Dapper;
using Hangfire;
using Hangfire.SQLite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Data;

namespace AutoTest.Infrastructure;

public static class AddInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddAutoTestInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        DapperTypeHandlers.EnsureInitialized();
        var hangfireConnection = configuration.GetConnectionString("HangfireConnection")
                                 ?? configuration["ConnectionStrings:HangfireConnection"]
                                 ?? "Data Source=hangfire.db;";
        hangfireConnection = hangfireConnection.Trim();
        if (!hangfireConnection.EndsWith(";", StringComparison.Ordinal))
            hangfireConnection += ";";

        services.AddSingleton<IWorkflowScheduler, HangfireWorkflowScheduler>();
        services.AddHangfire(config =>
        {
            config.UseSimpleAssemblyNameTypeSerializer()
                  .UseRecommendedSerializerSettings()
                  .UseSQLiteStorage(hangfireConnection);
        });
        services.AddHangfireServer();
        services.AddTransient<WorkflowJob>();
        services.AddHostedService<HangfireSchedulerInitializer>();
        services.AddScoped<IMonitorRepository, MonitorRepository>();
        services.AddScoped<IExecutionRecordRepository, ExecutionRecordRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddSingleton<RedisLockService>(sp =>
        {
            var redisConnection = "localhost:6379";
            return new RedisLockService(redisConnection);
        });
        return services;
    }
}

internal static class DapperTypeHandlers
{
    private static bool _initialized;

    public static void EnsureInitialized()
    {
        if (_initialized)
            return;

        SqlMapper.AddTypeHandler(new GuidTypeHandler());
        _initialized = true;
    }

    private sealed class GuidTypeHandler : SqlMapper.TypeHandler<Guid>
    {
        public override void SetValue(IDbDataParameter parameter, Guid value)
        {
            parameter.DbType = DbType.String;
            parameter.Value = value.ToString();
        }

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

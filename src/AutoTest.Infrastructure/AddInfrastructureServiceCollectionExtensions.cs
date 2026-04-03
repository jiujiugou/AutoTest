using AutoTest.Application;
using AutoTest.Core.Abstraction;
using Dapper;
using System.Data;
using Microsoft.Extensions.DependencyInjection;

namespace AutoTest.Infrastructure;

public static class AddInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddAutoTestInfrastructure(this IServiceCollection services)
    {
        DapperTypeHandlers.EnsureInitialized();
        services.AddScoped<IMonitorRepository, MonitorRepository>();
        services.AddScoped<IExecutionRecordRepository, ExecutionRecordRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddSingleton<IMonitorExecutionCoordinator, MonitorExecutionCoordinator>();
        services.AddSingleton<ITaskQueue, TaskQueue>(); // 任务队列通常是单例的
        services.AddHostedService<TaskWorker>();
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

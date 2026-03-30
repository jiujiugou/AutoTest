using AutoTest.Application;
using AutoTest.Core.Abstraction;
using Microsoft.Extensions.DependencyInjection;

namespace AutoTest.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddAutoTestInfrastructure(this IServiceCollection services)
    {
        // 这里可以注册基础设施层的服务，比如数据库上下文、仓储实现等
        services.AddScoped<IMonitorRepository, MonitorRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        return services;
    }
}

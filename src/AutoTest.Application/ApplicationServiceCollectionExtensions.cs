using AutoTest.Application.Builder.AssertionBuilder;
using AutoTest.Application.Builder.TargetBuilder;
using AutoTest.Application.Execution;
using AutoTest.Core.Abstraction;
using AutoTest.Core.Execution;
using Microsoft.Extensions.DependencyInjection;

namespace AutoTest.Application;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddAutoTestApplication(this IServiceCollection services)
    {
        // 注册 MonitorService 和它的接口
        services.AddScoped<IMonitorService, MonitorService>();
        services.AddScoped<ITargetBuilder, HttpTargetBuilder>();
        services.AddScoped<IAssertionBuilder, HttpAssertionBuilder>();
        services.AddScoped<IOrchestrator, Orchestrator>();
        return services;
    }
}

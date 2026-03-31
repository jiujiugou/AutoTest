using AutoTest.Application.Builder.AssertionBuilder;
using AutoTest.Application.Builder.TargetBuilder;
using AutoTest.Application.Execution;
using AutoTest.Application.ExecutionPipeline;
using AutoTest.Application.Step;
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
        services.AddScoped<ITargetMap, HttpTargetMap>();
        services.AddScoped<IAssertionMap, HttpAssertionMap>();
        services.AddScoped<IOrchestrator, Orchestrator>();
        services.AddScoped<IPipeline, Pipeline>();
        services.AddScoped<IPipelineStep, AssertionStep>();
        services.AddScoped<IPipelineStep, ExecutionStep>();
        services.AddScoped<AssertionEngine>();
        return services;
    }
}

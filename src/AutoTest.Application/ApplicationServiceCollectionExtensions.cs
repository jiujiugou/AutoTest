using AutoTest.Application.Builder.AssertionBuilder;
using AutoTest.Application.Builder;
using AutoTest.Application.Execution;
using AutoTest.Application.ExecutionPipeline;
using AutoTest.Application.Step;
using AutoTest.Core.Abstraction;
using Microsoft.Extensions.DependencyInjection;
using AutoTest.Core.Execution;
using AutoTest.Application.Mapper.TargetBuilder;
using AutoTest.Application.Mapper.AssertionMapper;

namespace AutoTest.Application;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddAutoTestApplication(this IServiceCollection services)
    {
        // 注册 MonitorService 和它的接口
        services.AddScoped<IMonitorService, MonitorService>();
        services.AddScoped<ITargetMap, TcpTargetMap>();
        services.AddScoped<IAssertionMap, TcpAssertionMap>();
        services.AddScoped<IAssertionMap, DbAssertionMap>();
        services.AddScoped<IAssertionMap, PythonAssertionMap>();
        services.AddScoped<ITargetMap, HttpTargetMap>();
        services.AddScoped<IAssertionMap, HttpAssertionMap>();
        services.AddScoped<IOrchestrator, Orchestrator>();
        services.AddScoped<ExecutionEngineResolver>();
        services.AddScoped<IPipeline, Pipeline>();
        services.AddScoped<IPipelineStep, ExecutionStep>();
        services.AddScoped<IPipelineStep, AssertionStep>();
        services.AddScoped<IAssertionRuleMap, AssertionRuleMap>();
        services.AddScoped<AssertionEngine>();
        return services;
    }
}

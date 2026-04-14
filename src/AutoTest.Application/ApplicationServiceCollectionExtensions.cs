using AutoTest.Application.Execution;
using AutoTest.Application.ExecutionPipeline;
using AutoTest.Application.Step;
using AutoTest.Core.Abstraction;
using Microsoft.Extensions.DependencyInjection;
using AutoTest.Core.Execution;

namespace AutoTest.Application;

/// <summary>
/// Application 层依赖注入注册入口。
/// </summary>
/// <remarks>
/// 负责注册应用编排相关的服务（Pipeline/Step/Orchestrator/MonitorService 等）。
/// 具体的执行/断言实现与配置映射由基础设施层装配。
/// </remarks>
public static class ApplicationServiceCollectionExtensions
{
    /// <summary>
    /// 注册 Application 层服务。
    /// </summary>
    public static IServiceCollection AddAutoTestApplication(this IServiceCollection services)
    {
        // 注册 MonitorService 和它的接口
        services.AddScoped<IMonitorService, MonitorService>();
        services.AddScoped<IOrchestrator, Orchestrator>();
        services.AddScoped<ExecutionEngineResolver>();
        services.AddScoped<IPipeline, Pipeline>();
        services.AddScoped<IPipelineStep, ExecutionStep>();
        services.AddScoped<IPipelineStep, AssertionStep>();
        services.AddScoped<AssertionEngine>();
        return services;
    }
}

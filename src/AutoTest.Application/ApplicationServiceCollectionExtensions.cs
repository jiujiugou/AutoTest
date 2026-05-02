using AutoTest.Application.Execution;
using AutoTest.Application.ExecutionPipeline;
using AutoTest.Application.Step;
using AutoTest.Core.Abstraction;
using AutoTest.Core.Execution;
using AutoTest.Core.ExecutionPipeline;
using Microsoft.Extensions.DependencyInjection;

namespace AutoTest.Application;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddAutoTestApplication(this IServiceCollection services)
    {
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

using AutoTest.Application.Execution;
using AutoTest.Application.Pipelines;
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
        services.AddScoped<ITestPlanService, TestPlanService>();
        services.AddScoped<ITestReportService, TestReportService>();
        services.AddScoped<ExecutionEngineResolver>();
        services.AddScoped<AssertionEngine>();

        // Pipeline steps
        services.AddScoped<ExecutionStep>();
        services.AddScoped<AssertionStep>();

        // Pipelines
        services.AddScoped<DefaultPipeline>();
        services.AddScoped<TemplatePipeline>();

        // Pipeline selector
        services.AddScoped<IPipeline>(sp =>
        {
            // 返回一个代理，Orchestrator 在运行时根据 IsTemplate 选择
            return new PipelineSelector(
                sp.GetRequiredService<DefaultPipeline>(),
                sp.GetRequiredService<TemplatePipeline>());
        });

        return services;
    }
}

/// <summary>
/// 运行时根据 Monitor.IsTemplate 选择对应管道。
/// </summary>
internal class PipelineSelector : IPipeline
{
    private readonly DefaultPipeline _default;
    private readonly TemplatePipeline _template;

    public PipelineSelector(DefaultPipeline @default, TemplatePipeline template)
    {
        _default = @default;
        _template = template;
    }

    public async Task ExecuteAsync(PipelineContext context)
    {
        var pipeline = context.Monitor.IsTemplate ? (IPipeline)_template : _default;
        await pipeline.ExecuteAsync(context);
    }
}

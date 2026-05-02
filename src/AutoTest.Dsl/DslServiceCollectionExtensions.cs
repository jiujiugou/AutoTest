using AutoTest.Core.ExecutionPipeline;
using AutoTest.Dsl;
using Microsoft.Extensions.DependencyInjection;

namespace AutoTest.Dsl;

public static class DslServiceCollectionExtensions
{
    public static IServiceCollection AddAutoTestDsl(this IServiceCollection services)
    {
        services.AddScoped<IDslParser, DslParser>();
        services.AddScoped<IPipelineStep, TemplateResolutionStep>();
        return services;
    }
}

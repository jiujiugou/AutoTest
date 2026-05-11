using AutoTest.Core.Dsl;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace AutoTest.Workflow;

public static class WorkflowServiceCollectionExtensions
{
    public static IServiceCollection AddAutoTestWorkflow(this IServiceCollection services, string redisConnectionString)
    {
        services.AddScoped<IDslParser, DslParser>();
        services.AddScoped<TemplateResolutionStep>();
        services.AddScoped<WorkflowExecutionStep>();
        services.AddSingleton<CircuitBreaker>();
        services.AddScoped<IProgressStore, RedisProgressStore>();
        services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(redisConnectionString));
        return services;
    }
}

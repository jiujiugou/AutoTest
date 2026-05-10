using AutoTest.Core.Dsl;
using AutoTest.Core.ExecutionPipeline;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace AutoTest.Orchestration;

public static class OrchestrationServiceCollectionExtensions
{
    public static IServiceCollection AddAutoTestOrchestration(this IServiceCollection services,string redisConnectionString)
    {
        services.AddSingleton<CircuitBreaker>();
        services.AddScoped<ExecutionEngine>();
        services.AddScoped<IPipelineStep, RuntimeOrchestrationStep>();
        services.AddScoped<IProgressStore, RedisProgressStore>();
        services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            return ConnectionMultiplexer.Connect(redisConnectionString);
        });
        return services;
    }
}

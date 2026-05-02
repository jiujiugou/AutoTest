using AutoTest.Core.Dsl;
using AutoTest.Core.Execution;
using Microsoft.Extensions.DependencyInjection;

namespace AutoTest.Execution.Http;

public static class AddExecutionHttp
{
    public static IServiceCollection AddHttpExecution(this IServiceCollection services)
    {
        services.AddScoped<IHttpClient, FlurlHttpClient>();
        services.AddScoped<IExecutionEngine, HttpExecutionEngine>();
        services.AddScoped<IStepExecutor, HttpStepExecutor>();
        return services;
    }
}

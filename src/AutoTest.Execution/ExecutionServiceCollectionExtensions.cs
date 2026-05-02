using AutoTest.Core.Dsl;
using AutoTest.Core.Execution;
using AutoTest.Execution.Db;
using AutoTest.Execution.Http;
using AutoTest.Execution.Python;
using AutoTest.Execution.Tcp;
using Microsoft.Extensions.DependencyInjection;

namespace AutoTest.Execution;

public static class ExecutionServiceCollectionExtensions
{
    public static IServiceCollection AddAutoTestExecution(this IServiceCollection services)
    {
        services.AddScoped<IStepExecutorResolver, StepExecutorResolver>();
        services.AddScoped<IExecutionEngine, DbExecutionEngine>();
        services.AddScoped<IStepExecutor, DbStepExecutor>();
        services.AddScoped<IHttpClient, FlurlHttpClient>();
        services.AddScoped<IExecutionEngine, HttpExecutionEngine>();
        services.AddScoped<IStepExecutor, HttpStepExecutor>();
        services.AddScoped<IExecutionEngine, PythonExecutionEngine>();
        services.AddScoped<IStepExecutor, PythonStepExecutor>();
        services.AddScoped<IExecutionEngine, TcpExecutionEngine>();
        services.AddScoped<IStepExecutor, TcpStepExecutor>();
        return services;
    }
}

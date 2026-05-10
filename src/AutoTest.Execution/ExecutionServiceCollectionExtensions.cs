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
        // 注册具体执行引擎（同时暴露为 IExecutionEngine，供 ExecutionEngineResolver 的 IEnumerable<IExecutionEngine> 注入）
        services.AddScoped<HttpExecutionEngine>();
        services.AddScoped<IExecutionEngine>(sp => sp.GetRequiredService<HttpExecutionEngine>());
        services.AddScoped<TcpExecutionEngine>();
        services.AddScoped<IExecutionEngine>(sp => sp.GetRequiredService<TcpExecutionEngine>());
        services.AddScoped<DbExecutionEngine>();
        services.AddScoped<IExecutionEngine>(sp => sp.GetRequiredService<DbExecutionEngine>());
        services.AddScoped<PythonExecutionEngine>();
        services.AddScoped<IExecutionEngine>(sp => sp.GetRequiredService<PythonExecutionEngine>());

        // StepExecutors 需要拿到正确的具体引擎类型
        services.AddScoped<IStepExecutor>(sp => new HttpStepExecutor(sp.GetRequiredService<HttpExecutionEngine>()));
        services.AddScoped<IStepExecutor>(sp => new TcpStepExecutor(sp.GetRequiredService<TcpExecutionEngine>()));
        services.AddScoped<IStepExecutor>(sp => new DbStepExecutor(sp.GetRequiredService<DbExecutionEngine>()));
        services.AddScoped<IStepExecutor>(sp => new PythonStepExecutor(sp.GetRequiredService<PythonExecutionEngine>()));

        services.AddScoped<IStepExecutorResolver, StepExecutorResolver>();
        services.AddScoped<IHttpClient, FlurlHttpClient>();
        return services;
    }
}

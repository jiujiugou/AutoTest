using AutoTest.Core.Dsl;
using AutoTest.Core.Execution;
using Microsoft.Extensions.DependencyInjection;

namespace AutoTest.Execution.Tcp;

public static class AddExecutionTcp
{
    public static IServiceCollection AddTcpExecution(this IServiceCollection services)
    {
        services.AddScoped<IExecutionEngine, TcpExecutionEngine>();
        services.AddScoped<IStepExecutor, TcpStepExecutor>();
        return services;
    }
}

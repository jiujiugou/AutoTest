using AutoTest.Core.Execution;
using Microsoft.Extensions.DependencyInjection;

namespace AutoTest.Execution.Http;

public static class AddExecutionHttp
{
    public static IServiceCollection AddHttpExecution(this IServiceCollection services)
    {
        services.AddHttpClient("DefaultHttpClient", client =>
        {
            client.BaseAddress = new Uri("http://localhost:5033"); // 默认地址，可通过配置覆盖
        })
        .ConfigureHttpClient(c => c.Timeout = TimeSpan.FromSeconds(10));
        services.AddScoped<IExecutionEngine, HttpExecutionEngine>();
        return services;
    }
}

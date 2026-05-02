using AutoTest.Core.Dsl;
using AutoTest.Core.Execution;
using Microsoft.Extensions.DependencyInjection;

namespace AutoTest.Execution.Python
{
    public static class PythonExecutionRegistration
    {
        public static IServiceCollection AddPythonExecution(this IServiceCollection services)
        {
            services.AddScoped<IExecutionEngine, PythonExecutionEngine>();
            services.AddScoped<IStepExecutor, PythonStepExecutor>();
            return services;
        }
    }
}

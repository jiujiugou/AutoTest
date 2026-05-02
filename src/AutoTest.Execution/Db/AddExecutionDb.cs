using AutoTest.Core.Dsl;
using AutoTest.Core.Execution;
using Microsoft.Extensions.DependencyInjection;

namespace AutoTest.Execution.Db
{
    public static class DbExecutionRegistration
    {
        public static IServiceCollection AddExecutionDb(this IServiceCollection services)
        {
            services.AddScoped<IExecutionEngine, DbExecutionEngine>();
            services.AddScoped<IStepExecutor, DbStepExecutor>();
            return services;
        }
    }
}

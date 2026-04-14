using AutoTest.Core.Execution;
using Microsoft.Extensions.DependencyInjection;

namespace AutoTest.Execution.Db
{
    public static class AddDbExecutionE
    {
        public static IServiceCollection AddExecutionDb(this IServiceCollection services)
        {
            services.AddScoped<IExecutionEngine, DbExecutionEngine>();
            return services;
        }
    }
}

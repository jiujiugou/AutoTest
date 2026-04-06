using AutoTest.Core.Execution;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;

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

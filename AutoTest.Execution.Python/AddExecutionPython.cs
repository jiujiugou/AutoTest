using AutoTest.Core.Execution;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;

namespace AutoTest.Execution.Python
{
    public static class AddExecutionPython
    {
        public static IServiceCollection AddPythonExecution(this IServiceCollection services)
        {
            services.AddScoped<IExecutionEngine, PythonExecutionEngine>();
            return services;
        }
    }
}

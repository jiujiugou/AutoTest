using AutoTest.Assertions;
using AutoTest.Core.Dsl;
using Microsoft.Extensions.DependencyInjection;

namespace AutoTest.Assertion
{
    public static class AddOperator
    {
        public static IServiceCollection AddOperatorAssertion(this IServiceCollection services)
        {
            services.AddSingleton<IOperator, DefaultOperator>();
            services.AddScoped<IStepAssertionEvaluator, StepAssertionEvaluator>();
            return services;
        }
    }
}

using AutoTest.Assertions;
using Microsoft.Extensions.DependencyInjection;

namespace AutoTest.Assertion
{
    public static class AddOperator
    {
        public static IServiceCollection AddOperatorAssertion(this IServiceCollection services)
        {

            services.AddSingleton<IOperator, DefaultOperator>();


            return services;
        }
    }
}

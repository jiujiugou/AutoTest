using AutoTest.Assertions;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;

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

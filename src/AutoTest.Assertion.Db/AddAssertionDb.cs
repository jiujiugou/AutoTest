using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace AutoTest.Assertion.Db
{
    public static class AddAssertionDb
    {
        public static IServiceCollection AddDbAssertion(this IServiceCollection services)
        {
            var fiTypes = Assembly.GetExecutingAssembly()
                    .GetTypes()
                    .Where(t => typeof(IField).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

            foreach (var type in fiTypes)
            {
                // 泛型注册为 IField
                services.AddSingleton(typeof(IField), type);
            }

            return services;
        }
    }
}

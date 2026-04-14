using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace AutoTest.Assertions.Http;

public static class AddAssertionHttp
{
    public static IServiceCollection AddHttpAssertion(this IServiceCollection services)
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

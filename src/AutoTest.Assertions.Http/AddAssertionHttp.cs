using AutoTest.Core.Assertion;
using Microsoft.Extensions.DependencyInjection;

namespace AutoTest.Assertions.Http;

public static class AddAssertionHttp
{
    public static IServiceCollection AddHttpAssertion(this IServiceCollection services)
    {
        services.AddScoped<IAssertion, HttpAssertion>();
        return services;
    }
}

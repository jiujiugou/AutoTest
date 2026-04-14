using Microsoft.Extensions.DependencyInjection;

namespace Auth
{
    public static class AddRbacExtensions
    {
        public static IServiceCollection AddRbac(this IServiceCollection services)
        {

            services.AddScoped<IRbacService, RbacService>();
            return services;
        }
    }
}

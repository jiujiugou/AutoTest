using Microsoft.Extensions.DependencyInjection;

namespace CacheCommons;

public static class AddCacheServiceExtension
{
    public static IServiceCollection AddCacheService(this IServiceCollection services)
    {
        services.AddSingleton<ICacheService, MemoryCacheService>();
        return services;
    }
}

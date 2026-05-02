using Microsoft.Extensions.DependencyInjection;

namespace LockCommons;

public static class LockServiceCollectionExtensions
{
    public static IServiceCollection AddRedisLock(this IServiceCollection services, string connectionString)
    {
        services.AddSingleton(new RedisLockService(connectionString));
        services.AddSingleton<IDistributedLock>(sp => new RedisDistributedLockAdapter(
            sp.GetRequiredService<RedisLockService>()));
        return services;
    }
}

internal class RedisDistributedLockAdapter : IDistributedLock
{
    private readonly RedisLockService _service;
    public RedisDistributedLockAdapter(RedisLockService service) => _service = service;

    public async Task<ILockHandle?> AcquireAsync(string key, TimeSpan? ttl = null)
    {
        var handle = (RedisLock)_service.CreateLock(key, ttl ?? TimeSpan.FromSeconds(30));
        var acquired = await handle.AcquireAsync();
        return acquired ? handle : null;
    }
}

using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;
namespace CacheCommons;

/*
缓存穿透 ✅
缓存雪崩 ✅（部分通过 TTL + 随机偏移 + 锁缓解）
缓存击穿 / 热点 key ✅
并发安全 ✅
可空安全 / 缓存空值 ✅
数据一致性（通过 TTL + factory + 可手动刷新）✅
*/
public class MemoryCacheService : ICacheService
{
    private readonly IMemoryCache _cache;
    // 针对热点 key 的防击穿锁
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    public MemoryCacheService(IMemoryCache cache)
    {
        _cache = cache;
    }

    public async Task<T?> GetOrCreateAsync<T>(
        string key,
        Func<Task<T?>> factory,
        TimeSpan? ttl = null)
    {
        if (_cache.TryGetValue(key, out T? cached))
            return cached;

        var sem = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));

        await sem.WaitAsync();
        try
        {
            if (_cache.TryGetValue(key, out cached))
                return cached;

            var result = await factory();

            var baseTtl = ttl ?? TimeSpan.FromMinutes(5);
            var jitter = TimeSpan.FromSeconds(Random.Shared.Next(0, 30));

            if (result == null)
            {
                // 👉 防穿透：短 TTL
                _cache.Set(key, result, TimeSpan.FromMinutes(1));
            }
            else
            {
                _cache.Set(key, result, baseTtl + jitter);
            }

            return result;
        }
        finally
        {
            sem.Release();

            // 👉 防内存泄漏
            if (sem.CurrentCount == 1)
            {
                _locks.TryRemove(key, out _);
                sem.Dispose();
            }
        }
    }
    public Task RemoveAsync(string key)
    {
        _cache.Remove(key);
        return Task.CompletedTask;
    }



}
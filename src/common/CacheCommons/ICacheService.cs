namespace CacheCommons;

public interface ICacheService
{
    /// <summary>
    /// 获取缓存，如果不存在，调用 factory 从数据库/其他来源加载并写入缓存
    /// 支持防击穿、穿透和 TTL
    /// </summary>
    Task<T?> GetOrCreateAsync<T>(string key, Func<Task<T?>> factory, TimeSpan? ttl = null);

    /// <summary>
    /// 删除缓存
    /// </summary>
    Task RemoveAsync(string key);
}

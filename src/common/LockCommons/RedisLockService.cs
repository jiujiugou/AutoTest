using StackExchange.Redis;

namespace LockCommons;

public class RedisLockService
{
    private readonly ConnectionMultiplexer _redis;

    public RedisLockService(string connectionString)
    {
        _redis = ConnectionMultiplexer.Connect(connectionString);
    }

    public ILockHandle CreateLock(string key, TimeSpan timeout)
    {
        var db = _redis.GetDatabase();
        return new RedisLock(db, key, timeout);
    }

    public async Task<bool> TrySetOnceAsync(string key, TimeSpan ttl)
    {
        var db = _redis.GetDatabase();
        return await db.StringSetAsync(key, "1", ttl, when: When.NotExists);
    }
}

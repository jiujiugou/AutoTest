using StackExchange.Redis;

namespace LockCommons;

public class RedisLock : ILockHandle
{
    private readonly IDatabase _db;
    private readonly string _lockKey;
    private readonly string _lockValue;
    private readonly TimeSpan _lockTimeout;
    private bool _hasLock;
    private Timer? _renewTimer;

    public RedisLock(IDatabase db, string lockKey, TimeSpan lockTimeout)
    {
        _db = db;
        _lockKey = lockKey;
        _lockTimeout = lockTimeout;
        _lockValue = Guid.NewGuid().ToString();
    }

    public async Task<bool> AcquireAsync()
    {
        _hasLock = await _db.StringSetAsync(
            _lockKey, _lockValue, _lockTimeout, when: When.NotExists);

        if (_hasLock)
        {
            _renewTimer = new Timer(
                async _ => await ExtendAsync(_lockTimeout),
                null, _lockTimeout / 2, _lockTimeout / 2);
        }

        return _hasLock;
    }

    public async Task<bool> ExtendAsync(TimeSpan extra)
    {
        if (!_hasLock) return false;

        const string script = """
            if redis.call('get', KEYS[1]) == ARGV[1] then
                return redis.call('pexpire', KEYS[1], ARGV[2])
            else
                return 0
            end
            """;

        var result = (int)await _db.ScriptEvaluateAsync(
            script,
            new RedisKey[] { _lockKey },
            new RedisValue[] { _lockValue, (long)extra.TotalMilliseconds });

        return result == 1;
    }

    public async Task ReleaseAsync()
    {
        if (!_hasLock) return;

        _renewTimer?.Dispose();

        const string script = """
            if redis.call('get', KEYS[1]) == ARGV[1] then
                return redis.call('del', KEYS[1])
            else
                return 0
            end
            """;

        await _db.ScriptEvaluateAsync(
            script, new RedisKey[] { _lockKey }, new RedisValue[] { _lockValue });
        _hasLock = false;
    }

    public async ValueTask DisposeAsync()
    {
        await ReleaseAsync();
        GC.SuppressFinalize(this);
    }
}

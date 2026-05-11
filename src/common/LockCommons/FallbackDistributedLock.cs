using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace LockCommons;

/// <summary>
/// 分布式锁兜底：优先 Redis，宕机时回退到进程内锁。
/// </summary>
public class FallbackDistributedLock : IDistributedLock
{
    private readonly IDistributedLock _primary;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locals = new();
    private readonly ILogger<FallbackDistributedLock> _logger;

    public FallbackDistributedLock(IDistributedLock primary, ILogger<FallbackDistributedLock> logger)
    {
        _primary = primary;
        _logger = logger;
    }

    public async Task<ILockHandle?> AcquireAsync(string key, TimeSpan? ttl = null)
    {
        try
        {
            var handle = await _primary.AcquireAsync(key, ttl);
            if (handle != null)
                return handle;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis 不可用，key={Key} 回退到本地锁", key);
        }

        // 本地锁 fallback
        var sem = _locals.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        var acquired = await sem.WaitAsync(TimeSpan.FromSeconds(5));
        if (!acquired)
            return null;

        return new LocalLockHandle(sem);
    }

    private sealed class LocalLockHandle : ILockHandle
    {
        private SemaphoreSlim? _sem;

        public LocalLockHandle(SemaphoreSlim sem) => _sem = sem;

        public Task<bool> AcquireAsync() => Task.FromResult(true);
        public Task<bool> ExtendAsync(TimeSpan extra) => Task.FromResult(true);

        public Task ReleaseAsync()
        {
            _sem?.Release();
            _sem = null;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            _sem?.Release();
            _sem = null;
            return ValueTask.CompletedTask;
        }
    }
}

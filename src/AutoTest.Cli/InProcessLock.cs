using System.Collections.Concurrent;
using LockCommons;

namespace AutoTest.Cli;

/// <summary>
/// 进程内分布式锁替代实现，用于 CLI 单进程场景。
/// </summary>
internal class InProcessLock : IDistributedLock
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    public async Task<ILockHandle?> AcquireAsync(string key, TimeSpan? ttl = null)
    {
        var semaphore = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        var acquired = await semaphore.WaitAsync(TimeSpan.FromSeconds(30));
        if (!acquired) return null;
        return new InProcessLockHandle(semaphore);
    }

    private sealed class InProcessLockHandle : ILockHandle
    {
        private SemaphoreSlim? _semaphore;
        private bool _acquired;

        public InProcessLockHandle(SemaphoreSlim semaphore)
        {
            _semaphore = semaphore;
            _acquired = true;
        }

        public Task<bool> AcquireAsync() => Task.FromResult(_acquired);

        public Task<bool> ExtendAsync(TimeSpan extra) => Task.FromResult(true);

        public Task ReleaseAsync()
        {
            if (_acquired)
            {
                _semaphore?.Release();
                _acquired = false;
            }
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            if (_acquired)
            {
                _semaphore?.Release();
                _acquired = false;
            }
            return ValueTask.CompletedTask;
        }
    }
}

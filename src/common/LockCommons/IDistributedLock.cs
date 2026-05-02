namespace LockCommons;

public interface IDistributedLock
{
    Task<ILockHandle?> AcquireAsync(string key, TimeSpan? ttl = null);
}

public interface ILockHandle : IAsyncDisposable
{
    Task<bool> AcquireAsync();
    Task<bool> ExtendAsync(TimeSpan extra);
    Task ReleaseAsync();
}

using System;
using System.Threading;
using System.Threading.Tasks;
using StackExchange.Redis;

namespace AutoTest.Infrastructure
{
    /// <summary>
    /// Redis 分布式锁：使用 SET NX + TTL 获取锁，使用 Lua 脚本校验 value 后续期/释放以保证原子性。
    /// </summary>
    public class RedisLock : IAsyncDisposable
    {
        private readonly IDatabase _db;
        private readonly string _lockKey;
        private readonly string _lockValue;
        private readonly TimeSpan _lockTimeout;

        private bool _hasLock = false;

        private Timer? _renewTimer;

        /// <summary>
        /// 初始化 <see cref="RedisLock"/>。
        /// </summary>
        /// <param name="db">Redis 数据库。</param>
        /// <param name="lockKey">锁 Key。</param>
        /// <param name="lockTimeout">锁 TTL。</param>
        public RedisLock(IDatabase db, string lockKey, TimeSpan lockTimeout)
        {
            _db = db;
            _lockKey = lockKey;
            _lockTimeout = lockTimeout;
            _lockValue = Guid.NewGuid().ToString();
        }

        /// <summary>
        /// 尝试获取锁
        /// </summary>
        /// <returns>成功获取锁则返回 true；否则返回 false。</returns>
        public async Task<bool> AcquireAsync()
        {
            _hasLock = await _db.StringSetAsync(
                _lockKey,
                _lockValue,
                _lockTimeout,
                when: When.NotExists
            );

            if (_hasLock)
            {
                // 开启续锁 Timer，每 TTL/2 续一次锁
                _renewTimer = new Timer(async _ => await ExtendAsync(_lockTimeout), null,
                                        _lockTimeout / 2, _lockTimeout / 2);
            }

            return _hasLock;
        }

        /// <summary>
        /// 续锁（延长 TTL）
        /// </summary>
        /// <param name="extra">新的 TTL（毫秒）。</param>
        /// <returns>续锁成功返回 true；否则返回 false。</returns>
        public async Task<bool> ExtendAsync(TimeSpan extra)
        {
            if (!_hasLock) return false;

            const string luaScript = @"
                if redis.call('get', KEYS[1]) == ARGV[1] then
                    return redis.call('pexpire', KEYS[1], ARGV[2])
                else
                    return 0
                end";

            var result = (int)await _db.ScriptEvaluateAsync(
                luaScript,
                new RedisKey[] { _lockKey },
                new RedisValue[] { _lockValue, (long)extra.TotalMilliseconds }
            );

            return result == 1;
        }

        /// <summary>
        /// 释放锁，Lua 原子删除
        /// </summary>
        public async Task ReleaseAsync()
        {
            if (!_hasLock) return;

            // 停止续锁 Timer
            _renewTimer?.Dispose();

            const string luaScript = @"
                if redis.call('get', KEYS[1]) == ARGV[1] then
                    return redis.call('del', KEYS[1])
                else
                    return 0
                end";

            await _db.ScriptEvaluateAsync(luaScript, new RedisKey[] { _lockKey }, new RedisValue[] { _lockValue });
            _hasLock = false;
        }

        /// <summary>
        /// 释放锁并清理资源。
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            await ReleaseAsync();
        }
    }
}

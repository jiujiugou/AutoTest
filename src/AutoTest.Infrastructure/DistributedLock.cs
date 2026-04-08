using System;
using System.Collections.Generic;
using System.Text;
using StackExchange.Redis;

namespace AutoTest.Infrastructure
{
    public class RedisLock : IDisposable
    {
        private readonly IDatabase _db;
        private readonly string _lockKey;
        private readonly string _lockValue;
        private readonly TimeSpan _lockTimeout;

        private bool _hasLock = false;

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
        public async Task<bool> AcquireAsync()
        {
            // SET key value NX PX timeout
            _hasLock = await _db.StringSetAsync(
                _lockKey,
                _lockValue,
                _lockTimeout,
                when: When.NotExists
            );
            return _hasLock;
        }

        /// <summary>
        /// 释放锁，Lua 原子删除
        /// </summary>
        public async Task ReleaseAsync()
        {
            if (!_hasLock) return;

            string luaScript = @"
                if redis.call('get', KEYS[1]) == ARGV[1] then
                    return redis.call('del', KEYS[1])
                else
                    return 0
                end";

            await _db.ScriptEvaluateAsync(luaScript, new RedisKey[] { _lockKey }, new RedisValue[] { _lockValue });
            _hasLock = false;
        }


        public void Dispose()
        {
            ReleaseAsync().GetAwaiter().GetResult();
        }
    }
}

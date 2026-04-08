using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Text;

namespace AutoTest.Infrastructure
{
    internal class RedisLockService
    {
        private readonly ConnectionMultiplexer _redis;

        public RedisLockService(string connectionString)
        {
            _redis = ConnectionMultiplexer.Connect(connectionString);
        }

        public RedisLock GetLock(string key, TimeSpan timeout)
        {
            var db = _redis.GetDatabase();
            return new RedisLock(db, key, timeout);
        }
    }
}

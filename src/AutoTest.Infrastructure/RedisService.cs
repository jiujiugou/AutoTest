using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Text;

namespace AutoTest.Infrastructure
{
    /// <summary>
    /// Redis 基础服务：提供分布式锁与幂等标记等能力，供调度执行流程使用。
    /// </summary>
    internal class RedisService
    {
        private readonly ConnectionMultiplexer _redis;

        /// <summary>
        /// 初始化 <see cref="RedisService"/>。
        /// </summary>
        /// <param name="connectionString">Redis 连接字符串。</param>
        public RedisService(string connectionString)
        {
            _redis = ConnectionMultiplexer.Connect(connectionString);
        }

        /// <summary>
        /// 获取一个基于 Redis Key 的分布式锁实例。
        /// </summary>
        /// <param name="key">锁 Key。</param>
        /// <param name="timeout">锁超时时间（TTL）。</param>
        /// <returns>锁对象。</returns>
        public RedisLock GetLock(string key, TimeSpan timeout)
        {
            var db = _redis.GetDatabase();
            return new RedisLock(db, key, timeout);
        }
        // 幂等检查 + 标记
        // 返回 true = 可以执行，false = 已经存在
        /// <summary>
        /// 幂等标记：仅当 key 不存在时设置一次，并设置 TTL。
        /// </summary>
        /// <param name="key">幂等 Key。</param>
        /// <param name="ttl">过期时间。</param>
        /// <returns>如果成功设置则为 true；如果已存在则为 false。</returns>
        public async Task<bool> TrySetOnceAsync(string key, TimeSpan ttl)
        {
            var db = _redis.GetDatabase();
            // 原子操作：如果 key 不存在则设置，存在就返回 false
            return await db.StringSetAsync(
                key,
                "1",
                ttl,
                when: When.NotExists
            );
        }
    }
}

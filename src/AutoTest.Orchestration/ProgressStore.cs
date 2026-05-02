using AutoTest.Core.Dsl;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace AutoTest.Orchestration
{
    internal class RedisProgressStore : IProgressStore
    {
        private readonly IDatabase _db;

        private static readonly JsonSerializerOptions Options = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        public RedisProgressStore(IConnectionMultiplexer redis)
        {
            _db = redis.GetDatabase();
        }

        private static string Key(string executionId)
            => $"dsl:progress:{executionId}";
        /// <summary>
        /// 标记任务完成
        /// </summary>
        /// <param name="executionId"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public async Task SaveAsync(DslRuntimeContext ctx)
        {
            var key = Key(ctx.ExecutionId);

            var payload = JsonSerializer.Serialize(ctx, Options);

            // 写入 + TTL（避免无限堆积）
            await _db.StringSetAsync(
                key,
                payload,
                TimeSpan.FromHours(6),
                When.Always,
                CommandFlags.FireAndForget
            );
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="executionId"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public async Task<DslRuntimeContext?> RestoreAsync(string executionId)
        {
            var key = Key(executionId);

            var value = await _db.StringGetAsync(key);

            if (value.IsNullOrEmpty)
                return null;

            var json = value.ToString();
            var obj = JsonSerializer.Deserialize<DslRuntimeContext>(json);
            return obj;
        }

        public async Task CompleteAsync(string executionId)
        {
            await _db.KeyDeleteAsync(Key(executionId));
        }
    }
}

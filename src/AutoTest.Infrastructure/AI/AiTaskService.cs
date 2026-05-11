using AutoTest.Application;
using AutoTest.Core.AI;
using Dapper;
using Microsoft.Extensions.Options;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AutoTest.Infrastructure.AI
{
    internal class AiTaskService : IAiTaskService
    {
        private readonly IDbConnection _connection;
        private readonly int _maxRetries;

        public AiTaskService(IDbConnection connection, IOptions<AiWorkerOptions> options)
        {
            _connection = connection;
            _maxRetries = options.Value.MaxRetries;
        }

        // =========================
        // 1. 入队
        // =========================
        public async Task EnqueueAsync(AiTask task, CancellationToken ct = default)
        {
            // 幂等检查：同一 BizId 不重复入队
            var existing = await _connection.QueryFirstOrDefaultAsync<Guid?>(
                "SELECT Id FROM AiTask WHERE BizId = @BizId",
                new { task.BizId });
            if (existing.HasValue)
                return;

            task.Id = Guid.NewGuid();
            task.Status = "Pending";
            task.Attempts = 0;
            task.NextRunAt = DateTime.UtcNow;
            task.CreatedAt = DateTime.UtcNow;

            const string sql = """
    INSERT INTO AiTask
    (Id, TaskType, BizId, InputJson, OutputJson, Attempts, Status, NextRunAt, LockedBy, LockedAt, Error, CreatedAt)
    VALUES
    (@Id, @TaskType, @BizId, @InputJson, @OutputJson, @Attempts, @Status, @NextRunAt, @LockedBy, @LockedAt, @Error, @CreatedAt)
    """;

            await _connection.ExecuteAsync(sql, task);
        }

        // =========================
        // 2. 批量抢任务（核心）
        // =========================
        public async Task<List<AiTask>> TakeBatchAsync(int batchSize, CancellationToken ct = default)
        {
            var now = DateTime.UtcNow;
            var workerId = Environment.MachineName;

            var ids = (await _connection.QueryAsync<Guid>("""
    SELECT Id FROM AiTask
    WHERE Status = 'Pending' AND NextRunAt <= @Now
    ORDER BY NextRunAt
    """, new { Now = now })).Take(batchSize).ToList();

            if (ids.Count == 0)
                return new List<AiTask>();

            // 乐观锁：只抢 Status = 'Pending' 的，防止多 Worker 抢同一任务
            await _connection.ExecuteAsync("""
    UPDATE AiTask
    SET Status = 'Processing', LockedBy = @Worker, LockedAt = @Now
    WHERE Id IN @Ids AND Status = 'Pending'
    """, new { Ids = ids, Worker = workerId, Now = now });

            // 回查真正抢到的行
            var result = await _connection.QueryAsync<AiTask>("""
    SELECT Id, TaskType, BizId, InputJson, OutputJson, Attempts, Status,
           NextRunAt, LockedBy, LockedAt, Error, CreatedAt
    FROM AiTask
    WHERE Id IN @Ids AND Status = 'Processing' AND LockedBy = @Worker
    """, new { Ids = ids, Worker = workerId });

            return result.ToList();
        }

        // =========================
        // 3. 成功
        // =========================
        public async Task MarkCompletedAsync(Guid id, string outputJson, CancellationToken ct = default)
        {
            const string sql = """
    UPDATE AiTask
    SET Status = 'Success',
        OutputJson = @OutputJson
    WHERE Id = @Id
    """;

            await _connection.ExecuteAsync(sql, new
            {
                Id = id,
                OutputJson = outputJson
            });
        }

        // =========================
        // 4. 失败（带重试）
        // =========================
        public async Task MarkFailedAsync(Guid id, string error, DateTime nextRunAt, CancellationToken ct = default)
        {
            const string sql = """
    UPDATE AiTask
    SET
        Attempts = Attempts + 1,
        Error = @Error,
        NextRunAt = @NextRunAt,
        Status =
            CASE
                WHEN Attempts + 1 >= @MaxRetries THEN 'DeadLetter'
                ELSE 'Pending'
            END
    WHERE Id = @Id
    """;

            await _connection.ExecuteAsync(sql, new
            {
                Id = id,
                Error = error,
                NextRunAt = nextRunAt,
                MaxRetries = _maxRetries
            });
        }
    }
}
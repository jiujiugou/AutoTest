using System.Data;
using AutoTest.Core.Abstraction;
using AutoTest.Core.Outbox;
using Dapper;

namespace AutoTest.Infrastructure.Outbox;

/// <summary>
/// Outbox 仓储（Dapper）：负责写入 outbox 消息，并提供基于数据库行锁语义的“批量认领/加锁”能力供分发器消费。
/// </summary>
public sealed class DapperOutboxRepository : IOutboxRepository
{
    private readonly IDbConnection _db;

    /// <summary>
    /// 初始化 <see cref="DapperOutboxRepository"/>。
    /// </summary>
    /// <param name="db">数据库连接。</param>
    public DapperOutboxRepository(IDbConnection db)
    {
        _db = db;
    }

    /// <summary>
    /// 写入一条 outbox 消息。
    /// </summary>
    /// <param name="message">消息。</param>
    /// <param name="tx">可选事务。</param>
    public Task AddAsync(OutboxMessage message, IDbTransaction? tx = null)
    {
        const string sql = """
                           INSERT INTO OutboxMessage
                           (Id, Type, PayloadJson, OccurredAt, Status, Attempts, NextAttemptAt, LockedUntil, LockedBy, LastError, SentAt)
                           VALUES
                           (@Id, @Type, @PayloadJson, @OccurredAt, @Status, @Attempts, @NextAttemptAt, @LockedUntil, @LockedBy, @LastError, @SentAt)
                           """;

        return _db.ExecuteAsync(sql, new
        {
            Id = message.Id.ToString(),
            message.Type,
            message.PayloadJson,
            message.OccurredAt,
            Status = (int)message.Status,
            message.Attempts,
            message.NextAttemptAt,
            message.LockedUntil,
            message.LockedBy,
            message.LastError,
            message.SentAt
        }, tx);
    }

    /// <summary>
    /// 认领并锁定下一批待处理消息。
    /// </summary>
    /// <param name="take">最多认领条数。</param>
    /// <param name="lockDuration">锁定时长。</param>
    /// <param name="lockedBy">锁持有者标识（实例/进程级）。</param>
    /// <param name="utcNow">当前 UTC 时间。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>本次成功认领的消息列表。</returns>
    public async Task<IReadOnlyList<OutboxMessage>> LockNextBatchAsync(
        int take,
        TimeSpan lockDuration,
        string lockedBy,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var result = new List<OutboxMessage>(take);
        var isSqlServer = _db is Microsoft.Data.SqlClient.SqlConnection;
        var selectSql = isSqlServer
            ? """
              SELECT TOP (@Take) Id
              FROM OutboxMessage
              WHERE (Status = @Pending OR Status = @Failed)
                AND (NextAttemptAt IS NULL OR NextAttemptAt <= @Now)
                AND (LockedUntil IS NULL OR LockedUntil <= @Now)
              ORDER BY OccurredAt ASC
              """
            : """
              SELECT Id
              FROM OutboxMessage
              WHERE (Status = @Pending OR Status = @Failed)
                AND (NextAttemptAt IS NULL OR NextAttemptAt <= @Now)
                AND (LockedUntil IS NULL OR LockedUntil <= @Now)
              ORDER BY OccurredAt ASC
              LIMIT @Take
              """;
        var ids = await _db.QueryAsync<string>(new CommandDefinition(
            selectSql,
            new
            {
                Pending = (int)OutboxStatus.Pending,
                Failed = (int)OutboxStatus.Failed,
                Now = utcNow,
                Take = take
            },
            cancellationToken: cancellationToken
        ));

        foreach (var id in ids)
        {
            var claimed = await _db.ExecuteAsync(new CommandDefinition(
                """
                UPDATE OutboxMessage
                SET Status = @Processing,
                    LockedUntil = @LockedUntil,
                    LockedBy = @LockedBy,
                    Attempts = Attempts + 1
                WHERE Id = @Id
                  AND (Status = @Pending OR Status = @Failed)
                  AND (NextAttemptAt IS NULL OR NextAttemptAt <= @Now)
                  AND (LockedUntil IS NULL OR LockedUntil <= @Now)
                """,
                new
                {
                    Id = id,
                    Processing = (int)OutboxStatus.Processing,
                    Pending = (int)OutboxStatus.Pending,
                    Failed = (int)OutboxStatus.Failed,
                    Now = utcNow,
                    LockedUntil = utcNow.Add(lockDuration),
                    LockedBy = lockedBy
                },
                cancellationToken: cancellationToken
            ));

            if (claimed != 1)
                continue;

            var msg = await _db.QueryFirstOrDefaultAsync<OutboxRow>(new CommandDefinition(
                """
                SELECT Id, Type, PayloadJson, OccurredAt, Status, Attempts, NextAttemptAt, LockedUntil, LockedBy, LastError, SentAt
                FROM OutboxMessage
                WHERE Id = @Id AND LockedBy = @LockedBy
                """,
                new { Id = id, LockedBy = lockedBy },
                cancellationToken: cancellationToken
            ));

            if (msg == null)
                continue;

            result.Add(msg.ToModel());
        }

        return result;
    }

    /// <summary>
    /// 将消息标记为已发送，并清理锁定信息。
    /// </summary>
    /// <param name="id">消息 ID。</param>
    /// <param name="lockedBy">锁持有者标识。</param>
    /// <param name="sentAt">发送时间（UTC）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    public Task MarkSentAsync(Guid id, string lockedBy, DateTime sentAt, CancellationToken cancellationToken)
    {
        return _db.ExecuteAsync(new CommandDefinition(
            """
            UPDATE OutboxMessage
            SET Status = @Sent,
                SentAt = @SentAt,
                LockedUntil = NULL,
                LockedBy = NULL,
                LastError = NULL,
                NextAttemptAt = NULL
            WHERE Id = @Id AND LockedBy = @LockedBy
            """,
            new
            {
                Id = id.ToString(),
                LockedBy = lockedBy,
                Sent = (int)OutboxStatus.Sent,
                SentAt = sentAt
            },
            cancellationToken: cancellationToken
        ));
    }

    /// <summary>
    /// 将消息标记为失败，并记录错误与下次重试时间，同时清理锁定信息。
    /// </summary>
    /// <param name="id">消息 ID。</param>
    /// <param name="lockedBy">锁持有者标识。</param>
    /// <param name="error">失败原因。</param>
    /// <param name="nextAttemptAt">下次重试时间（UTC）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    public Task MarkFailedAsync(Guid id, string lockedBy, string error, DateTime nextAttemptAt, CancellationToken cancellationToken)
    {
        return _db.ExecuteAsync(new CommandDefinition(
            """
            UPDATE OutboxMessage
            SET Status = @Failed,
                LockedUntil = NULL,
                LockedBy = NULL,
                LastError = @LastError,
                NextAttemptAt = @NextAttemptAt
            WHERE Id = @Id AND LockedBy = @LockedBy
            """,
            new
            {
                Id = id.ToString(),
                LockedBy = lockedBy,
                Failed = (int)OutboxStatus.Failed,
                LastError = error,
                NextAttemptAt = nextAttemptAt
            },
            cancellationToken: cancellationToken
        ));
    }

    private sealed class OutboxRow
    {
        public string Id { get; set; } = "";
        public string Type { get; set; } = "";
        public string PayloadJson { get; set; } = "";
        public DateTime OccurredAt { get; set; }
        public int Status { get; set; }
        public int Attempts { get; set; }
        public DateTime? NextAttemptAt { get; set; }
        public DateTime? LockedUntil { get; set; }
        public string? LockedBy { get; set; }
        public string? LastError { get; set; }
        public DateTime? SentAt { get; set; }

        public OutboxMessage ToModel()
        {
            return new OutboxMessage
            {
                Id = Guid.Parse(Id),
                Type = Type,
                PayloadJson = PayloadJson,
                OccurredAt = OccurredAt,
                Status = (OutboxStatus)Status,
                Attempts = Attempts,
                NextAttemptAt = NextAttemptAt,
                LockedUntil = LockedUntil,
                LockedBy = LockedBy,
                LastError = LastError,
                SentAt = SentAt
            };
        }
    }
}

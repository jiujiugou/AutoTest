using System.Data;
using AutoTest.Core.Outbox;

namespace AutoTest.Core.Abstraction;

/// <summary>
/// Outbox 仓储接口，用于在数据库中持久化与派发 <see cref="OutboxMessage"/>。
/// </summary>
public interface IOutboxRepository
{
    /// <summary>
    /// 新增一条 outbox 消息。
    /// </summary>
    /// <param name="message">要写入的消息。</param>
    /// <param name="tx">可选事务，用于与业务写入保持同一事务提交。</param>
    Task AddAsync(OutboxMessage message, IDbTransaction? tx = null);

    /// <summary>
    /// 领取下一批可派发的消息，并为其设置租约锁。
    /// </summary>
    /// <param name="take">批大小。</param>
    /// <param name="lockDuration">租约时长。</param>
    /// <param name="lockedBy">领取者标识。</param>
    /// <param name="utcNow">当前 UTC 时间（用于一致性判断）。</param>
    /// <param name="cancellationToken">取消标记。</param>
    Task<IReadOnlyList<OutboxMessage>> LockNextBatchAsync(int take,TimeSpan lockDuration,string lockedBy,DateTime utcNow,CancellationToken cancellationToken);

    /// <summary>
    /// 标记消息已成功发送。
    /// </summary>
    Task MarkSentAsync(Guid id, string lockedBy, DateTime sentAt, CancellationToken cancellationToken);

    /// <summary>
    /// 标记消息发送失败，并设置下一次重试时间。
    /// </summary>
    Task MarkFailedAsync(Guid id, string lockedBy, string error, DateTime nextAttemptAt, CancellationToken cancellationToken);
}

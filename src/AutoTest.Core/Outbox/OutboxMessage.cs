namespace AutoTest.Core.Outbox;

/// <summary>
/// Outbox 消息：用于在数据库事务内可靠记录“待对外发送”的事件，再由后台派发器异步投递。
/// </summary>
/// <remarks>
/// 该模型通常配合 Transactional Outbox 模式使用：
/// 业务事务内写入业务数据 + 写入 <see cref="OutboxMessage"/>（<see cref="OutboxStatus.Pending"/>），
/// 事务提交后由派发器轮询并发送 Webhook/消息队列。
/// </remarks>
public sealed class OutboxMessage
{
    /// <summary>
    /// 消息 ID（幂等键）。
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// 消息类型（事件名称）。
    /// </summary>
    public string Type { get; init; } = "";

    /// <summary>
    /// 消息载荷 JSON。
    /// </summary>
    public string PayloadJson { get; init; } = "";

    /// <summary>
    /// 事件发生时间（UTC）。
    /// </summary>
    public DateTime OccurredAt { get; init; }

    /// <summary>
    /// 当前状态（Pending/Processing/Sent/Failed）。
    /// </summary>
    public OutboxStatus Status { get; set; }

    /// <summary>
    /// 投递尝试次数。
    /// </summary>
    public int Attempts { get; set; }

    /// <summary>
    /// 下一次允许重试的时间（UTC）。
    /// </summary>
    public DateTime? NextAttemptAt { get; set; }

    /// <summary>
    /// 分布式租约到期时间（UTC）。
    /// </summary>
    public DateTime? LockedUntil { get; set; }

    /// <summary>
    /// 当前持有租约的实例标识。
    /// </summary>
    public string? LockedBy { get; set; }

    /// <summary>
    /// 最近一次失败的错误信息。
    /// </summary>
    public string? LastError { get; set; }

    /// <summary>
    /// 成功投递时间（UTC）。
    /// </summary>
    public DateTime? SentAt { get; set; }
}

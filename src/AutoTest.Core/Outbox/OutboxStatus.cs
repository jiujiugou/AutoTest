namespace AutoTest.Core.Outbox;

/// <summary>
/// Outbox 消息状态。
/// </summary>
public enum OutboxStatus
{
    /// <summary>
    /// 待处理（尚未被派发器领取）。
    /// </summary>
    Pending = 0,
    /// <summary>
    /// 处理中（已被某个派发器领取并加锁）。
    /// </summary>
    Processing = 1,
    /// <summary>
    /// 已发送（终态）。
    /// </summary>
    Sent = 2,
    /// <summary>
    /// 发送失败（可重试）。
    /// </summary>
    Failed = 3
}

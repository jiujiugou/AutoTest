namespace AutoTest.Infrastructure.Outbox;

/// <summary>
/// Outbox 分发配置。
/// </summary>
public sealed class OutboxOptions
{
    /// <summary>
    /// 轮询间隔（毫秒）。
    /// </summary>
    public int PollIntervalMs { get; set; } = 1000;

    /// <summary>
    /// 每次拉取的最大消息数。
    /// </summary>
    public int BatchSize { get; set; } = 20;

    /// <summary>
    /// 单条消息锁定时长（秒）。
    /// </summary>
    public int LockSeconds { get; set; } = 60;
}

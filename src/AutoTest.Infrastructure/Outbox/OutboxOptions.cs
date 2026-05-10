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

    /// <summary>
    /// 最大重试次数，超过后移入死信队列。默认 10。
    /// </summary>
    public int MaxRetryCount { get; set; } = 10;

    /// <summary>
    /// 死信消息保留天数，超期自动删除。默认 7 天。
    /// </summary>
    public int DeadLetterRetentionDays { get; set; } = 7;
}

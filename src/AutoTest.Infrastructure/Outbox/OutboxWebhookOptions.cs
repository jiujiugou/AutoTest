namespace AutoTest.Infrastructure.Outbox;

/// <summary>
/// Outbox Webhook 分发配置。
/// </summary>
public sealed class OutboxWebhookOptions
{
    /// <summary>
    /// 是否启用 Webhook 分发器。
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Webhook 接收地址。
    /// </summary>
    public string? Url { get; set; }

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
    /// HTTP 请求超时时间（秒）。
    /// </summary>
    public int TimeoutSeconds { get; set; } = 10;
}

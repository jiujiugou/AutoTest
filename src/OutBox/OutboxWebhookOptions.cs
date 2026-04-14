namespace OutBox;

public sealed class OutboxWebhookOptions
{
    public bool Enabled { get; set; }
    public string? Url { get; set; }
    public int PollIntervalMs { get; set; } = 1000;
    public int BatchSize { get; set; } = 20;
    public int LockSeconds { get; set; } = 60;
    public int TimeoutSeconds { get; set; } = 10;
}


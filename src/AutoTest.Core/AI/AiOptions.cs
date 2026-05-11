namespace AutoTest.Core.AI;

public class AiOptions
{
    public string Endpoint { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public string ModelId { get; set; } = "";
    public int MaxTokens { get; set; } = 4096;
    public double Temperature { get; set; } = 0.3;
}

public class AiWorkerOptions
{
    public int Parallelism { get; set; } = 4;
    public int BatchSize { get; set; } = 10;
    public int PollIntervalMs { get; set; } = 1000;
    public int ErrorDelayMs { get; set; } = 2000;
    public int EsWindowSeconds { get; set; } = 30;
    public int EsTake { get; set; } = 120;
    public int MaxRetries { get; set; } = 5;

    /// <summary>AI 熔断：连续失败次数阈值 (0 = 禁用)</summary>
    public int CircuitBreakerThreshold { get; set; } = 3;

    /// <summary>AI 熔断：断开后冷却时间（毫秒）</summary>
    public int CircuitBreakerCooldownMs { get; set; } = 60_000;
}

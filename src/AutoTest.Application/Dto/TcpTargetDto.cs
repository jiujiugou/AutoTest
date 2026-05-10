namespace AutoTest.Application.Dto;

public class TcpTargetDto
{
    public string Host { get; set; } = null!;
    public int Port { get; set; }
    public int Timeout { get; set; } = 30;
    public List<string> Messages { get; set; } = new();

    public bool UseTls { get; set; }
    public bool IgnoreSslErrors { get; set; }
    public int ConnectTimeoutMs { get; set; }
    public int ReadTimeoutMs { get; set; }
    public int WriteTimeoutMs { get; set; }
    public bool EnableRetry { get; set; }
    public int RetryCount { get; set; } = 2;
    public int RetryDelayMs { get; set; } = 500;
}

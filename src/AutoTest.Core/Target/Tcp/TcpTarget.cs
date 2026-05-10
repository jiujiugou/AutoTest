using System.Text.Json;
using System.Text.Json.Serialization;
using AutoTest.Core.Assertion;

namespace AutoTest.Core.Target;

public class TcpTarget : MonitorTarget
{
    public string Host { get; private set; } = null!;
    public int Port { get; private set; }
    public int Timeout { get; private set; } = 30;
    public List<string> Messages { get; private set; } = new();
    public List<AssertionResult>? Assertions { get; private set; }

    public bool UseTls { get; private set; }
    public bool IgnoreSslErrors { get; private set; }
    public int ConnectTimeoutMs { get; private set; } = 15000;
    public int ReadTimeoutMs { get; private set; } = 30000;
    public int WriteTimeoutMs { get; private set; } = 10000;
    public bool EnableRetry { get; private set; }
    public int RetryCount { get; private set; } = 2;
    public int RetryDelayMs { get; private set; } = 500;

    [JsonConstructor]
    public TcpTarget(
        string host,
        int port,
        int timeout = 30,
        List<string>? messages = null,
        bool useTls = false,
        bool ignoreSslErrors = false,
        int connectTimeoutMs = 0,
        int readTimeoutMs = 0,
        int writeTimeoutMs = 0,
        bool enableRetry = false,
        int retryCount = 2,
        int retryDelayMs = 500)
    {
        Host = host;
        Port = port;
        Timeout = timeout;
        if (messages != null) Messages = messages;
        UseTls = useTls;
        IgnoreSslErrors = ignoreSslErrors;
        var fallbackMs = timeout * 1000;
        ConnectTimeoutMs = connectTimeoutMs > 0 ? connectTimeoutMs : fallbackMs;
        ReadTimeoutMs = readTimeoutMs > 0 ? readTimeoutMs : fallbackMs;
        WriteTimeoutMs = writeTimeoutMs > 0 ? writeTimeoutMs : fallbackMs;
        EnableRetry = enableRetry;
        RetryCount = retryCount;
        RetryDelayMs = retryDelayMs;
    }

    public override string Type => "TCP";

    public override string ToJson() => JsonSerializer.Serialize(this);
}

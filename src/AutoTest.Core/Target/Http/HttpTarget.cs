using System.Text.Json;
using System.Text.Json.Serialization;

using AutoTest.Core;
using AutoTest.Core.Assertion;
using AutoTest.Core.http;
namespace AutoTest.Core.Target.Http;

public class HttpTarget : MonitorTarget
{
    [JsonInclude]
    public RequestMethod Method
    {
        get; private set;
    }

    [JsonInclude]
    public string Url { get; private set; } = null!;

    [JsonInclude]
    public HttpBody? Body
    {
        get; private set;
    }

    [JsonInclude]
    public Dictionary<string, string>? Headers
    {
        get; private set;
    }

    [JsonInclude]
    public Dictionary<string, string>? Query
    {
        get; private set;
    }
    public List<AssertionResult>? Assertions
    {
        get; private set;
    }
    [JsonInclude]
    public int Timeout { get; private set; } = 30;

    // 1. 认证
    [JsonInclude]
    public AuthType? AuthType
    {
        get; set;
    }
    [JsonInclude]
    public string? AuthToken
    {
        get; set;
    }
    [JsonInclude]
    public string? AuthUsername
    {
        get; set;
    }
    [JsonInclude]
    public string? AuthPassword
    {
        get; set;
    }

    // 2. Cookie 开关
    [JsonInclude]
    public bool UseCookies { get; set; } = true;

    // 3. 重定向策略
    [JsonInclude]
    public bool AllowAutoRedirect { get; set; } = true;
    [JsonInclude]
    public int MaxRedirects { get; set; } = 5;

    // 4. TLS/证书
    [JsonInclude]
    public bool IgnoreSslErrors { get; set; } = false;

    // 5. 代理
    [JsonInclude]
    public string? ProxyUrl
    {
        get; set;
    }
    [JsonInclude]
    public string? ProxyUser
    {
        get; set;
    }
    [JsonInclude]
    public string? ProxyPass
    {
        get; set;
    }

    // 6. 重试策略
    [JsonInclude]
    public bool EnableRetry { get; set; } = false;
    [JsonInclude]
    public int RetryCount { get; set; } = 2;
    [JsonInclude]
    public int RetryDelayMs { get; set; } = 500;

    // 7. 并发/大小限制（Flurl 可全局配置，这里开放开关）
    [JsonInclude]
    public bool EnableRateLimit { get; set; } = false;

    public override string Type => "HTTP";

    #region 构造函数（核心！）
    public HttpTarget()
    {
        // 自动初始化字典，避免运行时 Null 报错
        Query = new Dictionary<string, string>();
        Headers = new Dictionary<string, string>();
    }

    public HttpTarget(string url, RequestMethod method, HttpBody body) : this()
    {
        Url = url;
        Method = method;
        Body = body;
    }
    #endregion

    public override string ToJson()
    {
        return JsonSerializer.Serialize(this);
    }

}
public enum AuthType
{
    Bearer,
    Basic,
    ApiKeyHeader
}

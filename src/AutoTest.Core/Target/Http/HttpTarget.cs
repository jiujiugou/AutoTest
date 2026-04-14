using System.Text.Json;
using System.Text.Json.Serialization;

using AutoTest.Core;
using AutoTest.Core.Assertion;
using AutoTest.Core.http;
namespace AutoTest.Core.Target.Http;

/// <summary>
/// HTTP 监控目标：描述一次 HTTP 请求的完整配置（URL、方法、请求体/头、超时、认证、重试与限流等）。
/// </summary>
public class HttpTarget : MonitorTarget
{
    /// <summary>
    /// http请求方法
    /// </summary>
    [JsonInclude]
    public RequestMethod Method
    {
        get; private set;
    }
    /// <summary>
    /// url请求
    /// </summary>
    [JsonInclude]
    public string Url { get; private set; } = null!;
    /// <summary>
    /// 请求体
    /// </summary>
    [JsonInclude]
    public HttpBody? Body
    {
        get; private set;
    }
    /// <summary>
    /// 请求头
    /// </summary>
    [JsonInclude]
    public Dictionary<string, string[]>? Headers
    {
        get; private set;
    }
    /// <summary>
    /// url 查询参数
    /// </summary>
    [JsonInclude]
    public Dictionary<string, string>? Query
    {
        get; private set;
    }
    /// <summary>
    /// 断言结果
    /// </summary>
    public List<AssertionResult>? Assertions
    {
        get; private set;
    }
    /// <summary>
    /// 请求超时时间
    /// </summary>
    [JsonInclude]
    public int Timeout { get; private set; } = 30;

    // 1.认证
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
    public bool? UseCookies { get; set; }

    // 3. 重定向策略
    [JsonInclude]
    public bool? AllowAutoRedirect { get; set; }
    /// <summary>
    /// 最大重定向次数，默认 5 次，防止死循环
    /// </summary>
    [JsonInclude]
    public int MaxRedirects { get; set; } = 5;

    // 4. 是否忽略 SSL 证书错误（测试时可用）
    [JsonInclude]
    public bool? IgnoreSslErrors { get; set; }

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
        Headers = new Dictionary<string, string[]>();
    }

    public HttpTarget(string url, RequestMethod method) : this()
    {
        Url = url;
        Method = method;
    }

    public HttpTarget(
    string url,
    RequestMethod method,
    HttpBody? body = null,
    Dictionary<string, string[]>? headers = null,
    Dictionary<string, string>? query = null,
    int timeout = 30,
    AuthType? authType=null,
    string? authToken = null,
    string? authUsername = null,
    string? authPassword = null,
    bool? useCookies = null,
    bool? allowAutoRedirect=null,
    int maxRedirects = 5,
    bool? ignoreSslErrors=null,
    string? proxyUrl = null,
    string? proxyUser = null,
    string? proxyPass = null,
    bool enableRetry = false,
    int retryCount = 2,
    int retryDelayMs = 500,
    bool enableRateLimit = false
        ) : this()
    {
        Url = url;
        Method = method;
        Body = body;
        Headers = headers ?? new Dictionary<string, string[]>();
        Query = query ?? new Dictionary<string, string>();
        Timeout = timeout;
        AuthType = authType;
        AuthToken = authToken;
        AuthUsername = authUsername;
        AuthPassword = authPassword;
        UseCookies = useCookies;
        AllowAutoRedirect = allowAutoRedirect;
        MaxRedirects = maxRedirects;
        IgnoreSslErrors = ignoreSslErrors;
        ProxyUrl = proxyUrl;
        ProxyUser = proxyUser;
        ProxyPass = proxyPass;
        EnableRetry = enableRetry;
        RetryCount = retryCount;
        RetryDelayMs = retryDelayMs;
        EnableRateLimit = enableRateLimit;
    }
    #endregion

    public override string ToJson()
    {
        return JsonSerializer.Serialize(this);
    }

}
/// <summary>
/// HTTP 认证方式
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AuthType
{
    None,
    Bearer,
    Basic,
    ApiKeyHeader
}

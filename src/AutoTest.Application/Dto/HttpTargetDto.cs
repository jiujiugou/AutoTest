using AutoTest.Core.http;
using AutoTest.Core.Target.Http;

namespace AutoTest.Application.Dto;

/// <summary>
/// HTTP 测试任务 DTO
/// 用于接收前端或配置传入的测试参数
/// </summary>
public class HttpTargetDto
{
    /// <summary>
    /// HTTP 请求方法，如 GET、POST
    /// </summary>
    public RequestMethod Method { get; set; }

    /// <summary>
    /// 请求 URL
    /// </summary>
    public string Url { get; set; } = null!;

    /// <summary>
    /// 请求体
    /// </summary>
    public HttpBody? Body { get; set; }

    /// <summary>
    /// 请求头
    /// </summary>
    public Dictionary<string, object>? Headers { get; set; }

    /// <summary>
    /// 查询参数
    /// </summary>
    public Dictionary<string, string>? Query { get; set; }

    /// <summary>
    /// 超时时间（秒）
    /// </summary>
    public int Timeout { get; set; } = 30;

    /// <summary>
    /// 认证类型
    /// </summary>
    public AuthType AuthType { get; set; }

    public string? AuthToken { get; set; }
    public string? AuthUsername { get; set; }
    public string? AuthPassword { get; set; }

    /// <summary>
    /// 是否启用 Cookie
    /// </summary>
    public bool? UseCookies { get; set; } 

    /// <summary>
    /// 是否允许自动重定向
    /// </summary>
    public bool? AllowAutoRedirect { get; set; } = true;

    public int MaxRedirects { get; set; } = 5;
    public bool? IgnoreSslErrors { get; set; }

    /// <summary>
    /// 代理信息
    /// </summary>
    public string? ProxyUrl { get; set; }
    public string? ProxyUser { get; set; }
    public string? ProxyPass { get; set; }

    /// <summary>
    /// 是否启用重试
    /// </summary>
    public bool EnableRetry { get; set; } = false;
    public int RetryCount { get; set; } = 2;
    public int RetryDelayMs { get; set; } = 500;

    /// <summary>
    /// 是否启用全局并发限制
    /// </summary>
    public bool EnableRateLimit { get; set; } = true;
}

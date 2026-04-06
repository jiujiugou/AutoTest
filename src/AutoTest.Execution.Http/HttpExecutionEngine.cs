using AutoTest.Core;
using AutoTest.Core.Execution;
using AutoTest.Core.Target.Http;
using AutoTest.Execution.Http;
using Flurl;
using Flurl.Http;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Net;
using System.Text;
using System.Text.Json;

public class HttpExecutionEngine : IExecutionEngine
{
    private readonly ILogger<HttpExecutionEngine> _logger;
    // 轻量锁，避免同一 URL 的重复请求
    private readonly ConcurrentDictionary<string, byte> _inFlightUrl = new();
    // 全局并发限制信号量
    private static readonly SemaphoreSlim _globalSemaphore = new(5);

    public bool TryBegin(HttpTarget target)
    {
        var key = $"{target.Method}:{target.Url}";
        return _inFlightUrl.TryAdd(key, 0);
    }

    public void End(HttpTarget target)
    {
        var key = $"{target.Method}:{target.Url}";
        _inFlightUrl.TryRemove(key, out _);
    }
    public HttpExecutionEngine(ILogger<HttpExecutionEngine> logger)
    {

        _logger = logger;

    }
    public Task<ExecutionResult> ExecuteAsync(MonitorTarget target)
    {
        if (target is not HttpTarget httpTarget)
            throw new ArgumentException("目标类型不正确", nameof(target));

        return ExecuteAsync(httpTarget);
    }
    public async Task<ExecutionResult> ExecuteAsync(HttpTarget target)
    {
        if (!TryBegin(target))
        {
            _logger.LogWarning($"目标 {target.Url} 已在执行中，跳过本次请求");
            return new HttpExecutionResult(4044, null, false, "请求失败");
        }
        
        if (target.EnableRateLimit)
            await _globalSemaphore.WaitAsync(); // 等待全局信号量
        var stopwatch = Stopwatch.StartNew();
        try
        {
            for (int i = 0; i < target.RetryCount; i++)
            {

                try
                {
                    _logger.LogInformation($"开始执行 HTTP 请求 [{i}]，地址：{target.Url} 方法：{target.Method}");

                    var client = await GetOrCreateClient(target);

                    var request = client.Request(target.Url)
                        .SetQueryParams(target.Query ?? new Dictionary<string, string>())
                        .AllowAnyHttpStatus()
                        .WithTimeout(TimeSpan.FromSeconds(target.Timeout));

                    if (target.Headers != null)
                        request = request.WithHeaders(target.Headers);

                    var content = BuildHttpContent(target.Body);
                    var method = new HttpMethod(target.Method.ToString().ToUpper());

                    var response = await request.SendAsync(method, content, HttpCompletionOption.ResponseContentRead);

                    var body = await response.GetStringAsync();
                    var elapsedMs = stopwatch.ElapsedMilliseconds;

                    _logger.LogInformation($"HTTP 请求完成，状态码：{response.StatusCode} 耗时：{elapsedMs}ms");
                    return new HttpExecutionResult(
                        (int)response.StatusCode,
                         body,
                        true,
                        response.Headers.ToDictionary(h => h.Name, h => string.Join(",", h.Value)),
                        elapsedMs
                    );
                }
                catch (FlurlHttpException fex)
                {
                    var elapsedMs = stopwatch.ElapsedMilliseconds;
                    _logger.LogError(fex, $"HTTP 请求失败 [{i}]，耗时 {elapsedMs}ms");

                    if (!target.EnableRetry || i == target.RetryCount - 1)
                    {
                        var status = fex.Call?.Response?.StatusCode ?? 0;
                        var content = await fex.GetResponseStringAsync();
                        return new HttpExecutionResult(404, null, false, "请求失败");
                    }
                    else
                    {
                        _logger.LogInformation($"重试 {target.RetryDelayMs}ms 后再次尝试...");
                        await Task.Delay(target.RetryDelayMs);
                    }
                }

            }
        }
        catch (Exception ex)
        {
            var elapsedMs = stopwatch.ElapsedMilliseconds;
            _logger.LogError(ex, $"执行 HTTP 请求时发生异常，耗时 {elapsedMs}ms");
            
        }
        finally
        {
            if (target.EnableRateLimit)
                _globalSemaphore.Release();
            End(target);
        }
        return new HttpExecutionResult(404, null, false, "请求失败");
    }

    public bool CanExecute(MonitorTarget target) => target is HttpTarget;

    private HttpContent? BuildHttpContent(HttpBody? body)
    {
        if (body == null) return null;

        return body.Type switch
        {
            BodyType.Json => new StringContent(
                JsonSerializer.Serialize(body.Value),
                Encoding.UTF8,
                "application/json"
            ),

            BodyType.FormUrlEncoded => body.Value switch
            {
                Dictionary<string, string> dict => new FormUrlEncodedContent(dict),
                _ => throw new ArgumentException("FormUrlEncoded body.Value must be Dictionary<string,string>")
            },

            BodyType.Raw => new StringContent(
                body.Value?.ToString() ?? "",
                Encoding.UTF8,
                body.ContentType ?? "text/plain"
            ),

            _ => throw new NotSupportedException($"Body type {body.Type} not supported")
        };
    }
    private readonly ConcurrentDictionary<string, HttpClientHandler> _handlers = new();

    private Task<FlurlClient> GetOrCreateClient(HttpTarget target)
    {
        // Key 用来区分不同配置 + 认证信息
        var authPart = target.AuthType switch
        {
            AuthType.Bearer => target.AuthToken ?? "",
            AuthType.Basic => $"{target.AuthUsername}:{target.AuthPassword}",
            AuthType.ApiKeyHeader => target.AuthToken ?? "",
            _ => ""
        };

        var handlerKey = $"{target.AllowAutoRedirect}:{target.IgnoreSslErrors}:{target.ProxyUrl ?? ""}";
        var handler = _handlers.GetOrAdd(handlerKey, _ =>
        {
            var h = new HttpClientHandler
            {
                AllowAutoRedirect = target.AllowAutoRedirect,
                MaxAutomaticRedirections = target.MaxRedirects,
                UseCookies = target.UseCookies,
                ServerCertificateCustomValidationCallback = target.IgnoreSslErrors
                    ? HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                    : null
            };

            if (!string.IsNullOrEmpty(target.ProxyUrl))
            {
                h.Proxy = new WebProxy(target.ProxyUrl)
                {
                    Credentials = !string.IsNullOrEmpty(target.ProxyUser)
                        ? new NetworkCredential(target.ProxyUser, target.ProxyPass)
                        : null
                };
                h.UseProxy = true;
            }

            return h;
        });
        // 每次请求都创建新的 FlurlClient，安全写 header
        var client = new FlurlClient(new HttpClient(handler));

        switch (target.AuthType)
        {
            case AuthType.Bearer:
                if (!string.IsNullOrEmpty(target.AuthToken))
                    client.WithOAuthBearerToken(target.AuthToken);
                break;
            case AuthType.Basic:
                var auth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{target.AuthUsername}:{target.AuthPassword}"));
                client.WithHeader("Authorization", $"Basic {auth}");
                break;
            case AuthType.ApiKeyHeader:
                if (!string.IsNullOrEmpty(target.AuthToken))
                    client.WithHeader("X-Api-Key", target.AuthToken);
                break;
        }

        return Task.FromResult(client);
    }
}
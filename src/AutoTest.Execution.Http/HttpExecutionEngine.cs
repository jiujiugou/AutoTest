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
using System.Reflection.PortableExecutable;
using System.Text;
using System.Text.Json;

/// <summary>
/// HTTP 执行引擎，用于发送 HTTP 请求并返回结果，支持重试和并发控制。
/// </summary>
public class HttpExecutionEngine : IExecutionEngine
{
    private readonly ILogger<HttpExecutionEngine> _logger;
    private readonly IHttpClient _httpClient;
    // 全局并发限制信号量
    private static readonly SemaphoreSlim _globalSemaphore = new(5);

    public HttpExecutionEngine(ILogger<HttpExecutionEngine> logger, IHttpClient httpClient)
    {

        _logger = logger;
        _httpClient = httpClient;
    }
    /// <summary>
    /// 执行 HTTP 请求，适配 MonitorTarget 类型
    /// </summary>
    /// <param name="target">测试目标</param>
    /// <returns>ExecutionResult 执行结果</returns>
    public Task<ExecutionResult> ExecuteAsync(MonitorTarget target)
    {
        if (target is not HttpTarget httpTarget)
            throw new ArgumentException("目标类型不正确", nameof(target));

        return ExecuteAsync(httpTarget);
    }
    /// <summary>
    /// 执行 HTTP 请求，带重试和并发控制
    /// </summary>
    /// <param name="target">HTTP 测试目标</param>
    /// <returns>HTTP 执行结果</returns>
    public async Task<ExecutionResult> ExecuteAsync(HttpTarget target)
    {   
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

                    var client = await _httpClient.GetOrCreateClient(target);

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
                    // 1. 取响应头
                    var headersDict = response.Headers.GroupBy(h => h.Name, h => h.Value)
                        .ToDictionary(g => g.Key, g => g.ToArray());

                    
                    _logger.LogInformation($"HTTP 请求完成，状态码：{response.StatusCode} 耗时：{elapsedMs}ms");
                    return new HttpExecutionResult(
                        (int)response.StatusCode,
                         body,
                        true,
                        headersDict,
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
                        return new HttpExecutionResult(status, content, false, "请求失败");
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
            
        }
        return new HttpExecutionResult(404, null, false, "请求失败");
    }

    public bool CanExecute(MonitorTarget target) => target is HttpTarget;

    /// <summary>
    /// 根据 HttpBody 类型构建 HttpContent
    /// </summary>
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
    
}
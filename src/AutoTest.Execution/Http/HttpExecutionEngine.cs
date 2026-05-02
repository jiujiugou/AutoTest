using AutoTest.Core;
using AutoTest.Core.Execution;
using AutoTest.Core.Target.Http;
using AutoTest.Execution.Http;
using Flurl.Http;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;

public class HttpExecutionEngine : IExecutionEngine
{
    private readonly ILogger<HttpExecutionEngine> _logger;
    private readonly IHttpClient _httpClient;

    private static readonly ConcurrentDictionary<int, SemaphoreSlim> _semaphorePool = new();

    public HttpExecutionEngine(ILogger<HttpExecutionEngine> logger, IHttpClient httpClient)
    {
        _logger = logger;
        _httpClient = httpClient;
    }

    public bool CanExecute(MonitorTarget target) => target is HttpTarget;

    public Task<ExecutionResult> ExecuteAsync(MonitorTarget target)
    {
        if (target is not HttpTarget httpTarget)
            throw new ArgumentException("目标类型不正确", nameof(target));
        return ExecuteAsync(httpTarget);
    }

    public async Task<ExecutionResult> ExecuteAsync(HttpTarget target)
    {
        if (target.EnableRateLimit)
        {
            var semaphore = _semaphorePool.GetOrAdd(
                target.MaxConcurrency,
                _ => new SemaphoreSlim(target.MaxConcurrency));
            await semaphore.WaitAsync();
        }

        var stopwatch = Stopwatch.StartNew();
        try
        {
            for (int i = 0; i <= target.RetryCount; i++)
            {
                try
                {
                    _logger.LogInformation("HTTP 请求开始 [{i}/{max}]: {Method} {Url}",
                        i + 1, target.RetryCount + 1, target.Method, target.Url);

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

                    var headersDict = new Dictionary<string, string[]>();
                    foreach (var group in response.Headers.GroupBy(h => h.Name))
                        headersDict[group.Key] = group.Select(h => h.Value).ToArray();
                    if (response.ResponseMessage?.Content?.Headers != null)
                    {
                        foreach (var h in response.ResponseMessage.Content.Headers)
                            headersDict[h.Key] = h.Value.ToArray();
                    }

                    _logger.LogInformation("HTTP 请求完成: {Url}, Status={Status}, 耗时={Elapsed}ms",
                        target.Url, (int)response.StatusCode, elapsedMs);

                    return new HttpExecutionResult(
                        (int)response.StatusCode,
                        body,
                        true,
                        headersDict,
                        elapsedMs
                    );
                }
                catch (OperationCanceledException) when (!(i == target.RetryCount))
                {
                    _logger.LogWarning("HTTP 请求超时 [{i}/{max}]: {Url}，将在 {Delay}ms 后重试",
                        i + 1, target.RetryCount + 1, target.Url, target.RetryDelayMs);
                    await Task.Delay(target.RetryDelayMs);
                }
                catch (HttpRequestException hrex) when (!(i == target.RetryCount))
                {
                    _logger.LogWarning(hrex, "HTTP 网络异常 [{i}/{max}]: {Url}，将在 {Delay}ms 后重试",
                        i + 1, target.RetryCount + 1, target.Url, target.RetryDelayMs);
                    await Task.Delay(target.RetryDelayMs);
                }
                catch (FlurlHttpException fex)
                {
                    var elapsedMs = stopwatch.ElapsedMilliseconds;
                    _logger.LogError(fex, "HTTP 请求失败 [{i}/{max}]: {Url}，耗时 {Elapsed}ms",
                        i + 1, target.RetryCount + 1, target.Url, elapsedMs);

                    if (!target.EnableRetry || i == target.RetryCount)
                    {
                        var status = fex.Call?.Response?.StatusCode ?? 0;
                        var content = await fex.GetResponseStringAsync();
                        return new HttpExecutionResult((int)status, content, false, "请求失败");
                    }
                    else
                    {
                        _logger.LogInformation("将在 {Delay}ms 后重试...", target.RetryDelayMs);
                        await Task.Delay(target.RetryDelayMs);
                    }
                }
            }
        }
        catch (OperationCanceledException) when (!Debugger.IsAttached)
        {
            _logger.LogWarning("HTTP 请求最终超时: {Url}", target.Url);
            return new HttpExecutionResult(0, null, false, "请求超时");
        }
        catch (HttpRequestException hrex)
        {
            _logger.LogError(hrex, "HTTP 请求最终失败: {Url}", target.Url);
            return new HttpExecutionResult(0, null, false, $"网络异常: {hrex.Message}");
        }
        catch (Exception ex)
        {
            var elapsedMs = stopwatch.ElapsedMilliseconds;
            _logger.LogError(ex, "HTTP 请求异常: {Url}，耗时 {Elapsed}ms", target.Url, elapsedMs);
            return new HttpExecutionResult(0, null, false, $"请求异常: {ex.Message}");
        }
        finally
        {
            if (target.EnableRateLimit)
            {
                if (_semaphorePool.TryGetValue(target.MaxConcurrency, out var sem))
                    sem.Release();
            }
        }

        return new HttpExecutionResult(0, null, false, "重试耗尽");
    }

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
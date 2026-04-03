using System.Diagnostics;
using System.Text;
using System.Text.Json;
using AutoTest.Core;
using AutoTest.Core.Execution;
using AutoTest.Core.Target.Http;
using AutoTest.Execution.Http;
using Flurl;
using Flurl.Http;
using Microsoft.Extensions.Logging;

public class HttpExecutionEngine : IExecutionEngine
{
    private readonly ILogger<HttpExecutionEngine> _logger;

    public HttpExecutionEngine(ILogger<HttpExecutionEngine> logger)
    {
        _logger = logger;
    }

    public async Task<ExecutionResult> ExecuteAsync(HttpTarget target)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation($"Executing HTTP request: {target.Method} {target.Url}");

            // 1️⃣ 构建请求
            var request = target.Url
                .SetQueryParams(target.Query ?? new Dictionary<string, string>())
                .WithTimeout(TimeSpan.FromSeconds(target.Timeout))
                .AllowAnyHttpStatus();

            if (target.Headers != null)
            {
                request = request.WithHeaders(target.Headers);
            }

            // 2️⃣ 统一方法 + Body
            var method = new HttpMethod(target.Method.ToString().ToUpper());
            var content = BuildHttpContent(target.Body);

            // 🔥 核心：统一入口
            var response = await request.SendAsync(
                method,
                content,
                HttpCompletionOption.ResponseContentRead
            );

            // 3️⃣ 读取结果
            var body = await response.GetStringAsync();
            var elapsedMs = stopwatch.ElapsedMilliseconds;

            _logger.LogInformation($"HTTP finished: {response.StatusCode}, elapsed={elapsedMs}ms");

            return new HttpExecutionResult(
                (int)response.StatusCode,
                body,
                response.ResponseMessage.IsSuccessStatusCode
            )
            {
                ElapsedMilliseconds = elapsedMs
            };
        }
        catch (FlurlHttpException fex)
        {
            var status = fex.Call?.Response?.StatusCode ?? 0;
            var content = await fex.GetResponseStringAsync();
            var elapsedMs = stopwatch.ElapsedMilliseconds;

            _logger.LogError(fex, $"HTTP failed after {elapsedMs}ms");

            return new HttpExecutionResult(
                status,
                content,
                false,
                fex.Message
            )
            {
                ElapsedMilliseconds = elapsedMs
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "HTTP request failed");
            return new HttpExecutionResult(0, "", false, ex.Message);
        }
    }

    public bool CanExecute(MonitorTarget target) => target is HttpTarget;

    public Task<ExecutionResult> ExecuteAsync(MonitorTarget target)
    {
        if (target is not HttpTarget httpTarget)
            throw new ArgumentException("Invalid target type", nameof(target));

        return ExecuteAsync(httpTarget);
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

            BodyType.FormUrlEncoded => new FormUrlEncodedContent(
                (Dictionary<string, string>)body.Value!
            ),

            BodyType.Raw => new StringContent(
                body.Value?.ToString() ?? "",
                Encoding.UTF8,
                body.ContentType ?? "text/plain"
            ),

            _ => throw new NotSupportedException($"Body type {body.Type} not supported")
        };
    }
}
using System.Net.Http.Json;
using System.Text;
using AutoTest.Core;
using AutoTest.Core.Assertion;
using AutoTest.Core.Execution;
using AutoTest.Core.Target.Http;
using AutoTest.Logging;

namespace AutoTest.Execution.Http
{
    public class HttpExecutionEngine : IExecutionEngine
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<HttpExecutionEngine> _logger;
        public HttpExecutionEngine(IHttpClientFactory httpClientFactory, ILogger<HttpExecutionEngine> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task<ExecutionResult> ExecuteAsync(HttpTarget target)
        {
            var client = _httpClientFactory.CreateClient("DefaultHttpClient");
            // 构建请求（使用 UriBuilder + JsonContent 简化）
            var url = BuildUrl(target);
            _logger.LogInformation($"Executing HTTP request: {target.Method} {url}");
            using var request = new HttpRequestMessage(new HttpMethod(target.Method.ToString()), url);

            if (target.Headers != null)
                foreach (var h in target.Headers)
                    request.Headers.Add(h.Key, h.Value);

            if (!string.IsNullOrEmpty(target.Body))
                request.Content = new StringContent(target.Body, Encoding.UTF8, "application/json");

            try
            {
                var response = await client.SendAsync(request);
                var body = await response.Content.ReadAsStringAsync();
                _logger.LogInformation($"HTTP request finished: status={(int)response.StatusCode}, success={response.IsSuccessStatusCode}");
                _logger.LogDebug($"Response body: {body}");
                var result = new HttpExecutionResult(
                    (int)response.StatusCode,
                    body,
                    response.IsSuccessStatusCode
                );

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError($"HTTP request failed: {ex.Message}", ex);
                return new HttpExecutionResult(
                    0,
                    "",
                    false,
                    ex.Message
                );
            }
        }

        private string BuildUrl(HttpTarget target)
        {
            if (target.Query == null || target.Query.Count == 0)
                return target.Url;

            var query = string.Join("&", target.Query.Select(q => $"{q.Key}={Uri.EscapeDataString(q.Value)}"));
            return $"{target.Url}?{query}";
        }

        public bool CanExecute(MonitorTarget target) => target is HttpTarget;

        public Task<ExecutionResult> ExecuteAsync(MonitorTarget target)
        {
            if (target is not HttpTarget httpTarget)
                throw new ArgumentException("Invalid target type", nameof(target));

            return ExecuteAsync(httpTarget);
        }
    }
}
using AutoTest.Core.AI;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace AutoTest.AI;

public class ArkAiClient : IAiClient
{
    private readonly HttpClient _httpClient;
    private readonly AiOptions _options;
    private readonly ILogger<ArkAiClient> _logger;

    public ArkAiClient(HttpClient httpClient, IOptions<AiOptions> options, ILogger<ArkAiClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;

        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    public async Task<string> AnalyzeAsync(string prompt, CancellationToken ct = default)
    {
        var endpoint = _options.Endpoint;
        var apiKey = _options.ApiKey;
        var model = _options.ModelId;

        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(model))
            throw new InvalidOperationException("AI 配置不完整：请检查 AI:Endpoint, AI:ApiKey, AI:ModelId");

        var requestBody = new
        {
            model,
            temperature = _options.Temperature,
            max_tokens = _options.MaxTokens,
            messages = new[]
            {
                new { role = "user", content = prompt }
            }
        };

        for (int i = 0; i < 3; i++) // 简单重试
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
                request.Headers.Authorization =
                    new AuthenticationHeaderValue("Bearer", apiKey);

                request.Content = new StringContent(
                    JsonSerializer.Serialize(requestBody),
                    Encoding.UTF8,
                    "application/json"
                );

                var response = await _httpClient.SendAsync(request, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("AI调用失败: {Status} - {Content}", response.StatusCode, content);
                    throw new Exception("AI调用失败");
                }

                using var doc = JsonDocument.Parse(content);

                var result = doc.RootElement
                    .GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString();

                return result ?? "";
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "AI调用异常，第{Retry}次重试", i + 1);

                if (i == 2) throw;

                await Task.Delay(1000 * (i + 1), ct);
            }
        }

        throw new Exception("AI调用最终失败");
    }
}
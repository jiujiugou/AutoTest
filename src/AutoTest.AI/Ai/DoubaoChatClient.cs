using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;

namespace AutoTest.AI.Ai;

public class DoubaoChatClient : IChatClient
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _endpoint;

    public DoubaoChatClient(HttpClient httpClient, string apiKey, string endpoint)
    {
        _httpClient = httpClient;
        _apiKey = apiKey;
        _endpoint = endpoint;
    }
    public void Dispose()
    {
    }

    public async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (messages == null)
            throw new ArgumentNullException(nameof(messages));

        var request = BuildRequest(messages, options);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, _endpoint)
        {
            Content = CreateJsonContent(request)
        };

        httpRequest.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", _apiKey);

        var response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseContentRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);

        return ParseResponse(responseJson);
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        if (serviceType == typeof(HttpClient)) return _httpClient;
        if (serviceType.IsInstanceOfType(this)) return this;
        return null;
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (messages == null)
            throw new ArgumentNullException(nameof(messages));

        var request = BuildRequest(messages, options);
        request["stream"] = true;

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, _endpoint)
        {
            Content = CreateJsonContent(request)
        };

        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        using var response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line == null) break;
            if (line.Length == 0) continue;

            if (!line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                continue;

            var data = line.Substring(5).Trim();
            if (data == "[DONE]") yield break;

            if (TryParseStreamingDelta(data, out var deltaText, out var deltaRole))
            {
                if (!string.IsNullOrEmpty(deltaText))
                {
                    yield return new ChatResponseUpdate(deltaRole, deltaText);
                }
            }
        }
    }

    private static StringContent CreateJsonContent(object payload)
    {
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };
        return content;
    }

    private static Dictionary<string, object?> BuildRequest(IEnumerable<ChatMessage> messages, ChatOptions? options)
    {
        var msgList = messages.Select(m => new Dictionary<string, object?>
        {
            ["role"] = ToOpenAiRole(m.Role),
            ["content"] = m.Text ?? string.Empty
        }).ToList();

        if (!string.IsNullOrWhiteSpace(options?.Instructions))
        {
            msgList.Insert(0, new Dictionary<string, object?>
            {
                ["role"] = "system",
                ["content"] = options!.Instructions!
            });
        }

        var request = new Dictionary<string, object?>
        {
            ["messages"] = msgList
        };

        if (!string.IsNullOrWhiteSpace(options?.ModelId)) request["model"] = options.ModelId;
        if (options?.Temperature is not null) request["temperature"] = options.Temperature;
        if (options?.TopP is not null) request["top_p"] = options.TopP;
        if (options?.MaxOutputTokens is not null) request["max_tokens"] = options.MaxOutputTokens;
        if (options?.StopSequences is not null && options.StopSequences.Count > 0) request["stop"] = options.StopSequences;
        if (options?.PresencePenalty is not null) request["presence_penalty"] = options.PresencePenalty;
        if (options?.FrequencyPenalty is not null) request["frequency_penalty"] = options.FrequencyPenalty;
        if (options?.Seed is not null) request["seed"] = options.Seed;

        return request;
    }

    private static ChatResponse ParseResponse(string responseJson)
    {
        using var doc = JsonDocument.Parse(responseJson);
        var root = doc.RootElement;

        if (root.TryGetProperty("error", out var errorEl) && errorEl.ValueKind == JsonValueKind.Object)
        {
            var msg = errorEl.TryGetProperty("message", out var m) ? m.GetString() : "Unknown error";
            return new ChatResponse(new ChatMessage(ChatRole.Assistant, msg ?? "Unknown error"));
        }

        string content = "";
        if (root.TryGetProperty("choices", out var choices) && choices.ValueKind == JsonValueKind.Array && choices.GetArrayLength() > 0)
        {
            var choice0 = choices[0];
            if (choice0.TryGetProperty("message", out var msg) && msg.ValueKind == JsonValueKind.Object)
            {
                if (msg.TryGetProperty("content", out var c) && c.ValueKind == JsonValueKind.String)
                {
                    content = c.GetString() ?? "";
                }
            }
        }

        return new ChatResponse(new ChatMessage(ChatRole.Assistant, content));
    }

    private static bool TryParseStreamingDelta(string json, out string? deltaText, out ChatRole? deltaRole)
    {
        deltaText = null;
        deltaRole = null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("choices", out var choices) || choices.ValueKind != JsonValueKind.Array || choices.GetArrayLength() == 0)
                return false;

            var choice0 = choices[0];
            if (!choice0.TryGetProperty("delta", out var delta) || delta.ValueKind != JsonValueKind.Object)
                return false;

            if (delta.TryGetProperty("content", out var contentEl) && contentEl.ValueKind == JsonValueKind.String)
            {
                deltaText = contentEl.GetString();
            }

            if (delta.TryGetProperty("role", out var roleEl) && roleEl.ValueKind == JsonValueKind.String)
            {
                var r = roleEl.GetString();
                deltaRole = r?.Equals("assistant", StringComparison.OrdinalIgnoreCase) == true ? ChatRole.Assistant :
                            r?.Equals("user", StringComparison.OrdinalIgnoreCase) == true ? ChatRole.User :
                            r?.Equals("system", StringComparison.OrdinalIgnoreCase) == true ? ChatRole.System :
                            r?.Equals("tool", StringComparison.OrdinalIgnoreCase) == true ? ChatRole.Tool :
                            (ChatRole?)null;
            }

            return deltaText is not null || deltaRole is not null;
        }
        catch
        {
            return false;
        }
    }

    private static string ToOpenAiRole(ChatRole role)
    {
        var r = role.ToString();
        if (string.IsNullOrWhiteSpace(r)) return "user";
        return r.ToLowerInvariant();
    }
}

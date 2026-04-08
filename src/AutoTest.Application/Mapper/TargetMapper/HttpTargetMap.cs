using System.Text.Json;
using AutoTest.Application.Dto;
using AutoTest.Core;
using AutoTest.Core.Target.Http;

namespace AutoTest.Application.Builder;

public class HttpTargetMap : ITargetMap
{
    public string Type => "HTTP";  // 负责 HTTP 类型的 Target

    public MonitorTarget Map(string json)
    {
        // 将 json 反序列化成 DTO
        var dto = JsonSerializer.Deserialize<HttpTargetDto>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        })!;

        // 根据 DTO 构建实际的领域对象 HttpTarget
        return new HttpTarget(
    dto.Url,
    dto.Method,
    dto.Body,
    headers: NormalizeHeaders(dto.Headers),
    query: dto.Query,
    timeout: dto.Timeout,
    authType: dto.AuthType,
    authToken: dto.AuthToken,
    authUsername: dto.AuthUsername,
    authPassword: dto.AuthPassword,
    useCookies: dto.UseCookies,
    allowAutoRedirect: dto.AllowAutoRedirect,
    maxRedirects: dto.MaxRedirects,
    ignoreSslErrors: dto.IgnoreSslErrors,
    proxyUrl: dto.ProxyUrl,
    proxyUser: dto.ProxyUser,
    proxyPass: dto.ProxyPass,
    enableRetry: dto.EnableRetry,
    retryCount: dto.RetryCount,
    retryDelayMs: dto.RetryDelayMs,
    enableRateLimit: dto.EnableRateLimit);
    }
    private static Dictionary<string, string[]> NormalizeHeaders(Dictionary<string, object>? input)
    {
        var result = new Dictionary<string, string[]>();

        if (input == null) return result;

        foreach (var kv in input)
        {
            switch (kv.Value)
            {
                case string s:
                    result[kv.Key] = new[] { s };
                    break;

                case JsonElement je:
                    if (je.ValueKind == JsonValueKind.String)
                    {
                        result[kv.Key] = new[] { je.GetString()! };
                    }
                    else if (je.ValueKind == JsonValueKind.Array)
                    {
                        var list = new List<string>();
                        foreach (var item in je.EnumerateArray())
                        {
                            list.Add(item.GetString()!);
                        }
                        result[kv.Key] = list.ToArray();
                    }
                    break;

                case IEnumerable<string> arr:
                    result[kv.Key] = arr.ToArray();
                    break;

                default:
                    throw new InvalidOperationException($"Invalid header value for {kv.Key}");
            }
        }

        return result;
    }
}

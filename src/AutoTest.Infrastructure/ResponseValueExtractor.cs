using AutoTest.Core.Dsl;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AutoTest.Infrastructure;

internal class ResponseValueExtractor : IResponseValueExtractor
{
    public Task<Dictionary<string, string>> ExtractAsync(
        string body,
        IReadOnlyDictionary<string, string[]> headers,
        List<ValueExtractor> extractors)
    {
        var result = new Dictionary<string, string>();

        foreach (var ext in extractors)
        {
            string? value = ext.Source switch
            {
                ExtractSource.Body => ExtractFromBody(body, ext),
                ExtractSource.Header => ExtractFromHeader(headers, ext),
                _ => null
            };

            if (value != null)
                result[ext.Name] = value;
        }

        return Task.FromResult(result);
    }

    private static string? ExtractFromBody(string body, ValueExtractor ext)
    {
        if (string.IsNullOrEmpty(body)) return null;

        return ext.Method switch
        {
            ExtractMethod.JsonPath => ExtractJsonPath(body, ext.Expression),
            ExtractMethod.Regex => ExtractRegex(body, ext.Expression),
            _ => body
        };
    }

    private static string? ExtractFromHeader(IReadOnlyDictionary<string, string[]> headers, ValueExtractor ext)
    {
        if (headers == null) return null;

        var method = ext.Method == ExtractMethod.Regex ? ExtractMethod.Regex : ExtractMethod.Plain;

        if (method == ExtractMethod.Plain)
        {
            return headers.TryGetValue(ext.Expression, out var vals) ? string.Join(",", vals) : null;
        }

        foreach (var kv in headers)
        {
            foreach (var val in kv.Value)
            {
                var match = Regex.Match(val, ext.Expression);
                if (match.Success && match.Groups.Count > 1)
                    return match.Groups[1].Value;
            }
        }

        return null;
    }

    private static string? ExtractJsonPath(string body, string expression)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            var segments = expression.TrimStart('$').TrimStart('.').Split('.');
            JsonElement? current = doc.RootElement;

            foreach (var seg in segments)
            {
                if (seg.Contains('[') && seg.EndsWith(']'))
                {
                    var propName = seg[..seg.IndexOf('[')];
                    var indexStr = seg[(seg.IndexOf('[') + 1)..].TrimEnd(']');
                    if (!string.IsNullOrEmpty(propName) && current?.ValueKind == JsonValueKind.Object)
                        current = current.Value.GetProperty(propName);
                    if (int.TryParse(indexStr, out var idx) && current?.ValueKind == JsonValueKind.Array)
                        current = current.Value.EnumerateArray().ElementAtOrDefault(idx);
                    else
                        return null;
                }
                else if (current?.ValueKind == JsonValueKind.Object && current.Value.TryGetProperty(seg, out var prop))
                {
                    current = prop;
                }
                else
                {
                    return null;
                }
            }

            return current?.GetRawText().Trim('"');
        }
        catch
        {
            return null;
        }
    }

    private static string? ExtractRegex(string body, string pattern)
    {
        var match = Regex.Match(body, pattern);
        return match.Success && match.Groups.Count > 1 ? match.Groups[1].Value : null;
    }
}

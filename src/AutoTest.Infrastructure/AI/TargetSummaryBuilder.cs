using System.Text.Json;

namespace AutoTest.Infrastructure.AI;

internal static class TargetSummaryBuilder
{
    public static string? Build(string? targetType, string? targetConfig)
    {
        if (string.IsNullOrEmpty(targetConfig)) return null;

        try
        {
            using var doc = JsonDocument.Parse(targetConfig);
            var root = doc.RootElement;

            return targetType?.ToUpperInvariant() switch
            {
                "HTTP" => SummarizeHttp(root),
                "TCP" => SummarizeTcp(root),
                "DB" => SummarizeDb(root),
                "PYTHON" => SummarizePython(root),
                "TEMPLATE" => SummarizeTemplate(root),
                _ => null
            };
        }
        catch { return null; }
    }

    private static string? S(JsonElement r, string name) =>
        r.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static bool B(JsonElement r, string name) =>
        r.TryGetProperty(name, out var v) && v.GetBoolean();

    private static int I(JsonElement r, string name) =>
        r.TryGetProperty(name, out var v) ? v.GetInt32() : 0;

    // ── HTTP ──
    private static string SummarizeHttp(JsonElement r)
    {
        var auth = S(r, "AuthType") switch
        {
            "Bearer" => "BearerToken",
            "Basic" => $"Basic({S(r, "AuthUsername")})",
            "ApiKeyHeader" => "ApiKey",
            _ => null
        };

        return Join(
            $"HTTP {S(r, "Method") ?? "GET"} {S(r, "Url") ?? "?"}",
            I(r, "Timeout") > 0 ? $"Timeout:{I(r, "Timeout")}s" : null,
            auth != null ? $"Auth:{auth}" : null,
            auth == "BearerToken" ? $"Token首:{S(r, "AuthToken")?[..Math.Min(8, S(r, "AuthToken")?.Length ?? 0)]}..." : null,
            B(r, "EnableRetry") ? $"Retry:{I(r, "RetryCount")}x/{I(r, "RetryDelayMs")}ms" : null,
            B(r, "EnableRateLimit") ? "RateLimit:ON" : null,
            B(r, "IgnoreSslErrors") ? "SkipSSL" : null,
            S(r, "ProxyUrl") is { Length: > 0 } proxy ? $"Proxy:{proxy}" : null
        );
    }

    // ── TCP ──
    private static string SummarizeTcp(JsonElement r)
    {
        var host = S(r, "Host") ?? "?";
        var port = r.TryGetProperty("Port", out var p) ? p.GetInt32().ToString() : "?";
        var tls = B(r, "UseTls") ? "TLS" + (B(r, "IgnoreSslErrors") ? "(skipVerify)" : "") : "plain";
        var msgCount = r.TryGetProperty("Messages", out var msgs) ? msgs.GetArrayLength() : 0;

        return Join(
            $"TCP {host}:{port} ({tls})",
            $"ConnectTimeout:{I(r, "ConnectTimeoutMs")}ms Read:{I(r, "ReadTimeoutMs")}ms Write:{I(r, "WriteTimeoutMs")}ms",
            msgCount > 0 ? $"Messages:{msgCount}" : "portCheck",
            B(r, "EnableRetry") ? $"Retry:{I(r, "RetryCount")}x/{I(r, "RetryDelayMs")}ms" : null
        );
    }

    // ── DB ──
    private static string SummarizeDb(JsonElement r)
    {
        var sql = S(r, "Sql") ?? "";
        if (sql.Length > 150) sql = sql[..150] + "...";

        return Join(
            $"DB {S(r, "DbType") ?? "?"} {S(r, "CommandType") ?? "Query"}",
            $"Timeout:{I(r, "TimeoutSeconds")}s",
            $"SQL: {sql}",
            B(r, "EnableRetry") ? $"Retry:{I(r, "RetryCount")}x/{I(r, "RetryDelayMs")}ms" : null
        );
    }

    // ── Python ──
    private static string SummarizePython(JsonElement r)
    {
        var script = S(r, "ScriptPath") ?? "inline";
        var exe = S(r, "PythonExecutable") ?? "python";
        var argsLen = r.TryGetProperty("Args", out var args) ? args.GetArrayLength() : 0;

        return Join(
            $"Python {script} ({exe})",
            $"Timeout:{I(r, "TimeoutSeconds")}s",
            argsLen > 0 ? $"Args:{argsLen}" : null,
            B(r, "EnableRetry") ? $"Retry:{I(r, "RetryCount")}x/{I(r, "RetryDelayMs")}ms" : null,
            B(r, "EnableRateLimit") ? $"RateLimit:max={I(r, "MaxConcurrency")}" : null
        );
    }

    // ── Template (DSL) ──
    private static string? SummarizeTemplate(JsonElement r)
    {
        try
        {
            var lines = new List<string>();

            if (r.TryGetProperty("steps", out var steps))
            {
                int i = 1;
                foreach (var step in steps.EnumerateArray())
                {
                    var name = S(step, "name") ?? $"step{i}";
                    var type = S(step, "type") ?? "?";
                    var input = step.TryGetProperty("input", out var inp) ? inp : default;
                    var timeout = S(step, "timeout");

                    var detail = type.ToUpperInvariant() switch
                    {
                        "HTTP" => $"HTTP {S(input, "Method") ?? "GET"} {S(input, "Url") ?? "?"}",
                        "TCP" => $"TCP {S(input, "Host") ?? "?"}:{I(input, "Port")}",
                        "DB" => $"DB {S(input, "DbType") ?? "?"} {S(input, "CommandType") ?? "Query"}",
                        "PYTHON" => $"Python {S(input, "ScriptPath") ?? "inline"}",
                        _ => type
                    };

                    lines.Add($"  Step.{i} [{name}] {detail}{(timeout != null ? $" timeout={timeout}" : "")}");
                    i++;
                }
            }

            if (r.TryGetProperty("parallel", out var parallel))
            {
                int gi = 1;
                foreach (var group in parallel.EnumerateArray())
                {
                    var gname = S(group, "name") ?? $"group{gi}";
                    var mode = S(group, "mode") ?? "all";
                    var gsteps = group.TryGetProperty("steps", out var gs) ? gs.GetArrayLength() : 0;
                    lines.Add($"  Parallel.{gi} [{gname}] mode={mode} {gsteps} steps");
                    gi++;
                }
            }

            return lines.Count > 0 ? string.Join("\n", lines) : "Template";
        }
        catch { return "Template"; }
    }

    private static string Join(params string?[] parts) =>
        string.Join(" | ", parts.Where(x => x != null));
}

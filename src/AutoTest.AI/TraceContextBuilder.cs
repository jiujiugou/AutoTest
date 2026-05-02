using System.ComponentModel;
using System.Text;
using AutoTest.Application;
using AutoTest.Core.AI;
using Microsoft.SemanticKernel;

namespace AutoTest.AI;

public class TraceContextBuilder
{
    private readonly ILogService _logService;

    public TraceContextBuilder(ILogService logService)
    {
        _logService = logService;
    }

    [KernelFunction("trace_getContext")]
    [Description("根据 traceId 获取结构化的错误上下文日志（Markdown 格式），用于 LLM 分析错误原因和给出修复建议")]
    public async Task<string> BuildTraceContextAsync(
        [Description("Trace ID 标识符")] string traceId,
        [Description("错误发生时间（UTC），用于确定时间窗口")] DateTime? errorTime = null,
        [Description("时间窗口大小（秒），默认为 30")] int windowSeconds = 30,
        [Description("最多获取的日志条数，默认为 120")] int take = 120)
    {
        var logs = await _logService.GetAiErrorContextAsync(traceId, errorTime, windowSeconds, take);

        var sb = new StringBuilder();
        sb.AppendLine("## Trace Context");
        sb.AppendLine($"- **TraceId**: `{traceId}`");
        sb.AppendLine($"- **Error Time**: {errorTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "N/A"}");
        sb.AppendLine($"- **Time Window**: ±{windowSeconds}s");
        sb.AppendLine($"- **Log Entries**: {logs.Count}");
        sb.AppendLine();

        if (logs.Count > 0)
        {
            sb.AppendLine("### Log Timeline");
            sb.AppendLine("| # | Time | Service | Level | Message |");
            sb.AppendLine("|---|------|---------|-------|---------|");

            int index = 1;
            foreach (var log in logs)
            {
                var timeStr = log.Timestamp.ToLocalTime().ToString("HH:mm:ss");
                var svc = log.Service ?? "-";
                var message = log.Message.Replace("|", "\\|").Replace("\n", " ").Replace("\r", "");
                if (message.Length > 150) message = message[..150] + "...";
                sb.AppendLine($"| {index} | {timeStr} | {svc} | {log.Level} | {message} |");
                index++;
            }

            var errors = logs.Where(l => l.Level is "ERROR" or "FATAL").ToList();
            if (errors.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("### Error Details");
                foreach (var err in errors)
                {
                    sb.AppendLine($"- **Level**: {err.Level}");
                    sb.AppendLine($"  - **Time**: {err.Timestamp.ToLocalTime():yyyy-MM-dd HH:mm:ss}");
                    sb.AppendLine($"  - **Message**: {err.Message}");
                    if (!string.IsNullOrEmpty(err.Exception))
                    {
                        var ex = err.Exception.Length > 1000 ? err.Exception[..1000] + "..." : err.Exception;
                        sb.AppendLine($"  - **Exception**:");
                        sb.AppendLine("```");
                        sb.AppendLine(ex);
                        sb.AppendLine("```");
                    }
                    sb.AppendLine();
                }
            }
        }
        else
        {
            sb.AppendLine("_No log entries found for the given trace ID._");
        }

        return sb.ToString();
    }
}

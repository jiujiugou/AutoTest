using System.Text;
using AutoTest.Core.AI;

namespace AutoTest.Infrastructure.AI
{
    internal static class AiAnalysisPromptBuilder
    {
        public const string PromptVersion = "v1.1";

        public static string BuildSystemPrompt()
        {
            return """
你是分布式系统故障分析专家。

任务：
基于错误快照 + 关键日志，分析根因并输出 JSON。

要求：
- 不编造信息
- 信息不足返回 Unknown
- 只输出 JSON

字段：
type, severity, summary, rootCause, suggestion, impact, faultService, confidence, errorChain
""";
        }

        public static string BuildUserPrompt(AiAnalysisInputDto input, TraceContextData traceData)
        {
            var sb = new StringBuilder();

            sb.AppendLine("# 【错误快照】");
            sb.AppendLine($"ExceptionType: {input.ExceptionType ?? "N/A"}");
            sb.AppendLine($"Message: {input.ErrorMessage ?? "N/A"}");
            sb.AppendLine($"TraceId: {input.TraceId ?? "N/A"}");

            if (!string.IsNullOrWhiteSpace(input.StackTrace))
            {
                sb.AppendLine("\n# StackTrace");
                sb.AppendLine(input.StackTrace);
            }

            if (input.FailedAssertions?.Count > 0)
            {
                sb.AppendLine("\n# Failed Assertions");
                foreach (var a in input.FailedAssertions)
                {
                    sb.AppendLine($"- {a.Target}: {a.Message}");
                }
            }

            sb.AppendLine("\n# 【Trace 时间范围】");
            sb.AppendLine($"Window: ±{traceData.WindowSeconds}s");

            if (traceData.StartTime != null && traceData.EndTime != null)
            {
                sb.AppendLine($"Range: {traceData.StartTime} → {traceData.EndTime}");
            }

            // 🔥 关键：只给 AI “关键事件”，而不是全部日志
            sb.AppendLine("\n# 【关键错误事件（重点）】");

            if (traceData.KeyEvents?.Count > 0)
            {
                foreach (var log in traceData.KeyEvents)
                {
                    sb.AppendLine($"[{log.Timestamp:HH:mm:ss}] {log.Service} [{log.Level}] {log.Message}");
                }
            }
            else
            {
                sb.AppendLine("No key events found.");
            }

            sb.AppendLine("\n# 【完整日志摘要】");

            if (traceData.Logs?.Count > 0)
            {
                foreach (var log in traceData.Logs.Take(80)) // 🔥 限制长度
                {
                    var msg = log.Message
                        .Replace("\n", " ")
                        .Replace("\r", "")
                        .Replace("|", " ");

                    if (msg.Length > 120)
                        msg = msg[..120] + "...";

                    sb.AppendLine($"- {log.Timestamp:HH:mm:ss} [{log.Service}] {log.Level}: {msg}");
                }
            }

            sb.AppendLine(@"
# 输出格式
{
  ""type"": ""TestFailure|ApiError|PerformanceIssue|SecurityIssue"",
  ""severity"": ""low|medium|high|critical"",
  ""summary"": ""<100字"",
  ""rootCause"": ""<分析根因>"",
  ""suggestion"": ""<修复建议>"",
  ""impact"": ""single_request|module_level|system_level"",
  ""faultService"": ""服务名"",
  ""confidence"": 0.0-1.0,
  ""errorChain"": [
    {
      ""service"": ""服务名"",
      ""type"": ""trigger|failure|consequence"",
      ""detail"": ""说明""
    }
  ]
}
");

            return sb.ToString();
        }
    }


}

using System;

namespace AutoTest.Core.AI
{
    /// <summary>
    /// 追踪日志条目
    /// </summary>
    public class TraceLogEntry
    {
        public DateTime Timestamp { get; set; }

        public string Level { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;

        public string? Exception { get; set; }

        public string? Service { get; set; }
    }
}

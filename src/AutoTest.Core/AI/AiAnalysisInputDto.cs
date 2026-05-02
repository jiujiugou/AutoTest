using System;
using System.Collections.Generic;

namespace AutoTest.Core.AI
{
    /// <summary>
    /// AI 分析输入裁剪 DTO
    /// </summary>
    public class AiAnalysisInputDto
    {
        public string? ExceptionType { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime DateTime { get; set; }
        public string? StackTrace { get; set; }
        public Guid ExecutionId { get; set; }
        public string? TraceId { get; set; }
        public List<AssertionSummary>? FailedAssertions { get; set; }
    }

    public class AssertionSummary
    {
        public string Target { get; set; } = string.Empty;
        public string? Message { get; set; }
    }
}

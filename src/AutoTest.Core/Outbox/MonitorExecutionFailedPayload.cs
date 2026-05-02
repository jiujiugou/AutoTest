using AutoTest.Core.Assertion;
using System;
using System.Collections.Generic;
using System.Text;

namespace AutoTest.Core.Outbox
{
    public class MonitorExecutionFailedPayload
    {
        public Guid MonitorId { get; init; }
        public string MonitorName { get; init; } = "";

        public Guid ExecutionId { get; init; }

        public DateTime StartedAt { get; init; }
        public DateTime FinishedAt { get; init; }

        public FailureType FailureType { get; init; }

        public bool IsExecutionSuccess { get; init; }
        public bool IsAssertionSuccess { get; init; }

        public string? ErrorMessage { get; init; }

        public ExceptionInfo? Exception { get; init; }

        public List<AssertionResult>? Assertions { get; init; }

        public int Attempts { get; init; } // 可选（未来用）
    }
    public class ExceptionInfo
    {
        public string Type { get; init; } = "";
        public string Message { get; init; } = "";
        public string? StackTrace { get; init; }
    }
    public enum FailureType
    {
        Execution,
        Assertion,
        Exception
    }
}

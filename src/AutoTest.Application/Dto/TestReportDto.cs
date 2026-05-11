using AutoTest.Core.Assertion;

namespace AutoTest.Application.Dto;

public class TestReportDto
{
    public Guid PlanRunId { get; set; }
    public Guid TestPlanId { get; set; }
    public string TestPlanName { get; set; } = null!;
    public DateTime ExecutedAt { get; set; }
    public int TotalCount { get; set; }
    public int PassCount { get; set; }
    public int FailCount { get; set; }
    public double PassRate { get; set; }
    public long DurationMinMs { get; set; }
    public long DurationMaxMs { get; set; }
    public long DurationAvgMs { get; set; }
    public List<MonitorReportItem> Items { get; set; } = new();
}

public class MonitorReportItem
{
    public Guid MonitorId { get; set; }
    public string MonitorName { get; set; } = null!;
    public Guid ExecutionId { get; set; }
    public bool Passed { get; set; }
    public int StatusCode { get; set; }
    public long DurationMs { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public List<AssertionResult> Assertions { get; set; } = new();
}

public class PlanRunSummaryDto
{
    public Guid PlanRunId { get; set; }
    public DateTime ExecutedAt { get; set; }
    public int TotalCount { get; set; }
    public int PassCount { get; set; }
    public int FailCount { get; set; }
    public double PassRate { get; set; }
}

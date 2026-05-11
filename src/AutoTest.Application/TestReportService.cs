using System.Text;
using AutoTest.Application.Dto;
using AutoTest.Core.Abstraction;
using AutoTest.Core.Repositories;

namespace AutoTest.Application;

public class TestReportService : ITestReportService
{
    private readonly ITestPlanRepository _planRepository;
    private readonly IMonitorRepository _monitorRepository;
    private readonly IExecutionRecordRepository _executionRepository;

    public TestReportService(
        ITestPlanRepository planRepository,
        IMonitorRepository monitorRepository,
        IExecutionRecordRepository executionRepository)
    {
        _planRepository = planRepository;
        _monitorRepository = monitorRepository;
        _executionRepository = executionRepository;
    }

    public async Task<TestReportDto> GenerateReportAsync(Guid testPlanId, Guid planRunId)
    {
        var plan = await _planRepository.GetByIdAsync(testPlanId)
            ?? throw new InvalidOperationException("TestPlan not found");

        var records = (await _executionRepository.GetByPlanRunIdAsync(planRunId)).ToList();

        var items = new List<MonitorReportItem>();
        var durations = new List<long>();

        foreach (var record in records)
        {
            var monitor = await _monitorRepository.GetByIdAsync(record.MonitorId);
            var duration = record.FinishedAt.HasValue
                ? (long)(record.FinishedAt.Value - record.StartedAt).TotalMilliseconds
                : 0;

            var assertions = await _executionRepository.GetAssertionResultsAsync(record.Id);

            items.Add(new MonitorReportItem
            {
                MonitorId = record.MonitorId,
                MonitorName = monitor?.Name ?? "(已删除)",
                ExecutionId = record.Id,
                Passed = record.IsExecutionSuccess && assertions.All(a => a.IsSuccess),
                StatusCode = record.IsExecutionSuccess ? 200 : 0,
                DurationMs = duration,
                ErrorMessage = record.ErrorMessage,
                StartedAt = record.StartedAt,
                FinishedAt = record.FinishedAt,
                Assertions = assertions.ToList()
            });

            if (duration > 0) durations.Add(duration);
        }

        return new TestReportDto
        {
            PlanRunId = planRunId,
            TestPlanId = testPlanId,
            TestPlanName = plan.Name,
            ExecutedAt = records.FirstOrDefault()?.StartedAt ?? DateTime.UtcNow,
            TotalCount = items.Count,
            PassCount = items.Count(i => i.Passed),
            FailCount = items.Count(i => !i.Passed),
            PassRate = items.Count > 0 ? (double)items.Count(i => i.Passed) / items.Count : 0,
            DurationMinMs = durations.Count > 0 ? durations.Min() : 0,
            DurationMaxMs = durations.Count > 0 ? durations.Max() : 0,
            DurationAvgMs = durations.Count > 0 ? (long)durations.Average() : 0,
            Items = items
        };
    }

    public async Task<IEnumerable<PlanRunSummaryDto>> ListPlanRunsAsync(Guid testPlanId, int take = 20)
    {
        // Query distinct PlanRunIds from ExecutionRecord for monitors in this plan
        var plan = await _planRepository.GetByIdAsync(testPlanId);
        if (plan == null) return Enumerable.Empty<PlanRunSummaryDto>();

        var summaries = new List<PlanRunSummaryDto>();
        var seenPlanRunIds = new HashSet<Guid>();

        foreach (var monitorId in plan.MonitorIds)
        {
            var records = await _executionRepository.GetByMonitorIdAsync(monitorId, take: 50);
            foreach (var r in records)
            {
                if (r.PlanRunId == null || !seenPlanRunIds.Add(r.PlanRunId.Value))
                    continue;

                // Get all records for this plan run to aggregate
                var planRecords = await _executionRepository.GetByPlanRunIdAsync(r.PlanRunId.Value);
                var planRecordList = planRecords.ToList();
                if (planRecordList.Count == 0) continue;

                var passCount = planRecordList.Count(er => er.IsExecutionSuccess);

                summaries.Add(new PlanRunSummaryDto
                {
                    PlanRunId = r.PlanRunId.Value,
                    ExecutedAt = planRecordList.Min(er => er.StartedAt),
                    TotalCount = planRecordList.Count,
                    PassCount = passCount,
                    FailCount = planRecordList.Count - passCount,
                    PassRate = planRecordList.Count > 0 ? (double)passCount / planRecordList.Count : 0
                });
            }
        }

        return summaries.OrderByDescending(s => s.ExecutedAt).Take(take);
    }

    public string GenerateHtmlReport(TestReportDto report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html lang=\"zh-CN\"><head><meta charset=\"UTF-8\">");
        sb.AppendLine("<title>AutoTest 测试报告</title>");
        sb.AppendLine("<style>");
        sb.AppendLine("body{font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,sans-serif;max-width:900px;margin:2rem auto;padding:0 1.5rem;color:#1a1a2e;line-height:1.6}");
        sb.AppendLine(".pass{color:#16a34a}.fail{color:#dc2626}.bar{height:8px;border-radius:4px;background:#e5e7eb;margin:0.5rem 0}");
        sb.AppendLine(".bar-fill{height:100%;background:#16a34a;border-radius:4px}");
        sb.AppendLine("table{width:100%;border-collapse:collapse;margin:1rem 0;font-size:0.9rem}");
        sb.AppendLine("th,td{border:1px solid #e0e0e0;padding:0.5rem 0.75rem;text-align:left}");
        sb.AppendLine("th{background:#f0f4ff;font-weight:600}");
        sb.AppendLine("h1{border-bottom:2px solid #e0e0e0;padding-bottom:0.5rem}");
        sb.AppendLine("</style></head><body>");

        sb.AppendLine($"<h1>AutoTest 测试报告</h1>");
        sb.AppendLine($"<p><strong>测试计划：</strong>{report.TestPlanName} &nbsp;|&nbsp; <strong>执行时间：</strong>{report.ExecutedAt:yyyy-MM-dd HH:mm:ss} UTC</p>");

        // Summary
        var passRate = (report.PassRate * 100).ToString("F1");
        sb.AppendLine($"<h2>概览</h2>");
        sb.AppendLine($"<p>总计 <strong>{report.TotalCount}</strong> 项，");
        sb.AppendLine($"通过 <strong class=\"pass\">{report.PassCount}</strong>，");
        sb.AppendLine($"失败 <strong class=\"fail\">{report.FailCount}</strong> &nbsp;|&nbsp; 通过率 <strong>{passRate}%</strong></p>");
        sb.AppendLine($"<div class=\"bar\"><div class=\"bar-fill\" style=\"width:{passRate}%\"></div></div>");
        sb.AppendLine($"<p>耗时: 最快 {report.DurationMinMs}ms / 平均 {report.DurationAvgMs}ms / 最慢 {report.DurationMaxMs}ms</p>");

        // Item table
        sb.AppendLine("<h2>明细</h2>");
        sb.AppendLine("<table><tr><th>监控名称</th><th>状态</th><th>耗时</th><th>错误信息</th></tr>");
        foreach (var item in report.Items)
        {
            var statusClass = item.Passed ? "pass" : "fail";
            var statusText = item.Passed ? "通过" : "失败";
            sb.AppendLine($"<tr><td>{item.MonitorName}</td><td class=\"{statusClass}\">{statusText}</td><td>{item.DurationMs}ms</td><td>{item.ErrorMessage ?? "-"}</td></tr>");
        }
        sb.AppendLine("</table>");

        sb.AppendLine("<footer style=\"margin-top:3rem;padding-top:1rem;border-top:1px solid #e0e0e0;text-align:center;color:#888;font-size:0.85rem\">AutoTest · 自动生成</footer>");
        sb.AppendLine("</body></html>");
        return sb.ToString();
    }
}

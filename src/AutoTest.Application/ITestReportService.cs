using AutoTest.Application.Dto;

namespace AutoTest.Application;

public interface ITestReportService
{
    Task<TestReportDto> GenerateReportAsync(Guid testPlanId, Guid planRunId);
    string GenerateHtmlReport(TestReportDto report);
    Task<IEnumerable<PlanRunSummaryDto>> ListPlanRunsAsync(Guid testPlanId, int take = 20);
}

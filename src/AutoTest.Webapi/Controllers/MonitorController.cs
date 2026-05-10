using AutoTest.Application;
using AutoTest.Application.Dto;
using AutoTest.Core.Repositories;
using AutoTest.Infrastructure.AI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AutoTest.Webapi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MonitorController : ControllerBase
{
    private readonly IMonitorService _monitorService;
    private readonly IWorkflowScheduler _workflowScheduler;
    private readonly IAnalysisRepository _analysisRepository;

    public MonitorController(IMonitorService monitorService, IWorkflowScheduler workflowScheduler,
        IAnalysisRepository analysisRepository)
    {
        _monitorService = monitorService;
        _workflowScheduler = workflowScheduler;
        _analysisRepository = analysisRepository;
    }

    [HttpGet("list")]
    [Authorize(Policy = "perm:api.monitor.view")]
    public async Task<IActionResult> List([FromQuery] int take = 50)
    {
        var items = await _monitorService.ListAsync(take);
        var result = items.Select(m => new
        {
            m.Id,
            m.Name,
            TargetType = m.Target.Type,
            Status = (int)m.Status,
            m.LastRunTime,
            m.IsEnabled,
            AssertionCount = m.Assertions.Count,
            m.AutoDailyEnabled,
            m.AutoDailyTime,
            m.MaxRuns,
            m.ExecutedCount,
            m.IsTemplate
        });
        return Ok(result);
    }

    //根据ID查询
    [HttpGet("{id}")]
    [Authorize(Policy = "perm:api.monitor.view")]
    public async Task<IActionResult> Get(Guid id)
    {
        var result = await _monitorService.GetByIdAsync(id);
        if (result == null)
            return NotFound();

        return Ok(new
        {
            result.Id,
            result.Name,
            TargetType = result.Target.Type,
            TargetConfig = result.Target.ToJson(),
            result.IsEnabled,
            result.AutoDailyEnabled,
            result.AutoDailyTime,
            result.MaxRuns,
            result.ExecutedCount,
            result.IsTemplate,
            result.TemplateVariablesJson,
            AssertionCount = result.Assertions.Count,
            Assertions = result.Assertions.Select(a => new { a.Id, a.Type, a.ConfigJson })
        });
    }

    //创建

    [HttpPost]
    [Authorize(Policy = "perm:api.monitor.create")]
    public async Task<IActionResult> Add([FromBody] MonitorDto dto)
    {
        var id = await _monitorService.AddAsync(dto);
        var sch = await _monitorService.GetScheduleAsync(id);
        await ApplyScheduleAsync(id, dto.IsEnabled, sch.AutoDailyEnabled, sch.AutoDailyTime);
        return Ok(id);
    }

    //删除
    [HttpDelete("{id}")]
    [Authorize(Policy = "perm:api.monitor.delete")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _monitorService.DeleteAsync(id);
        await _workflowScheduler.RemoveMonitorScheduleAsync(id);
        return NoContent();
    }
    //更新
    [HttpPut("{id}")]
    [Authorize(Policy = "perm:api.monitor.update")]
    public async Task<IActionResult> Update(Guid id, [FromBody] MonitorDto dto)
    {
        await _monitorService.UpdateAsync(id, dto);
        var sch = await _monitorService.GetScheduleAsync(id);
        await ApplyScheduleAsync(id, dto.IsEnabled, sch.AutoDailyEnabled, sch.AutoDailyTime);
        return NoContent(); // 更新成功但不返回内容
    }

    public sealed record SetEnabledRequest(bool IsEnabled);

    [HttpPut("{id}/enabled")]
    [Authorize(Policy = "perm:api.monitor.update")]
    public async Task<IActionResult> SetEnabled(Guid id, [FromBody] SetEnabledRequest req)
    {
        await _monitorService.SetEnabledAsync(id, req.IsEnabled);
        var sch = await _monitorService.GetScheduleAsync(id);
        if (req.IsEnabled && sch.AutoDailyEnabled && !string.IsNullOrWhiteSpace(sch.AutoDailyTime))
            await _workflowScheduler.UpsertDailyMonitorAsync(id, sch.AutoDailyTime!);
        else
            await _workflowScheduler.RemoveMonitorScheduleAsync(id);
        return NoContent();
    }

    [HttpGet("{id}/executions/latest")]
    public async Task<IActionResult> GetLatestExecution(Guid id)
    {
        var record = await _monitorService.GetLatestExecutionAsync(id);
        if (record == null)
            return NotFound();

        var assertions = await _monitorService.GetExecutionAssertionResultsAsync(record.Id);
        return Ok(new { record, assertions });
    }

    [HttpGet("{id}/executions")]
    public async Task<IActionResult> GetExecutions(Guid id, [FromQuery] int take = 20)
    {
        var records = await _monitorService.GetExecutionsAsync(id, take);
        return Ok(records);
    }

    [HttpGet("{id}/runtime-stats")]
    public async Task<IActionResult> GetRuntimeStats(Guid id, [FromQuery] int topErrors = 10)
    {
        var result = await _monitorService.GetMonitorRuntimeStatsAsync(id, topErrors);
        return Ok(new { stats = result.Stats, topErrors = result.TopErrors });
    }

    [HttpGet("executions/{executionId}/assertions")]
    public async Task<IActionResult> GetExecutionAssertions(Guid executionId)
    {
        var assertions = await _monitorService.GetExecutionAssertionResultsAsync(executionId);
        return Ok(assertions);
    }
    [HttpPost("{id}/run")]
    [Authorize(Policy = "perm:api.monitor.run")]
    public async Task<IActionResult> TaskRun(Guid id)
    {
        var userId = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

        // Honor explicit client-provided key; otherwise use deterministic key for dedup
        var idempotencyKey = Request.Headers.TryGetValue("Idempotency-Key", out var headerVal)
            ? headerVal.ToString()
            : !string.IsNullOrWhiteSpace(userId)
                ? $"{id}:{userId}:{DateTime.UtcNow:yyyyMMddHHmm}"
                : Guid.NewGuid().ToString("N");

        await _workflowScheduler.RunNowAsync(id, userId, idempotencyKey);
        return Ok(new { idempotencyKey });
    }

    [HttpGet("executions/{executionId}/analysis")]
    public async Task<IActionResult> GetExecutionAnalysis(Guid executionId)
    {
        var analysis = await _analysisRepository.GetByExecutionRecordIdAsync(executionId);
        if (analysis == null)
            return NotFound(new { message = "暂无 AI 分析结果" });
        return Ok(new
        {
            analysis.Id,
            analysis.Type,
            analysis.Severity,
            analysis.Category,
            analysis.RootCause,
            analysis.Suggestion,
            analysis.Summary,
            analysis.Confidence,
            analysis.CreatedAt
        });
    }

    [HttpGet("{monitorId}/analysis-list")]
    public async Task<IActionResult> GetAnalysisList(Guid monitorId, [FromQuery] int take = 20)
    {
        var list = await _analysisRepository.GetByMonitorIdAsync(monitorId, take);
        return Ok(list.Select(a => new
        {
            a.Id,
            a.ExecutionRecordId,
            a.Type,
            a.Severity,
            a.Category,
            a.Summary,
            a.Confidence,
            a.CreatedAt
        }));
    }

    private Task ApplyScheduleAsync(Guid monitorId, bool isEnabled, bool autoDailyEnabled, string? autoDailyTime)
    {
        if (isEnabled && autoDailyEnabled && !string.IsNullOrWhiteSpace(autoDailyTime))
            return _workflowScheduler.UpsertDailyMonitorAsync(monitorId, autoDailyTime);

        return _workflowScheduler.RemoveMonitorScheduleAsync(monitorId);
    }
}

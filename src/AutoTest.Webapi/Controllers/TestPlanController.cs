using AutoTest.Application;
using AutoTest.Application.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AutoTest.Webapi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TestPlanController : ControllerBase
{
    private readonly ITestPlanService _testPlanService;
    private readonly ITestReportService _reportService;

    public TestPlanController(ITestPlanService testPlanService, ITestReportService reportService)
    {
        _testPlanService = testPlanService;
        _reportService = reportService;
    }

    [HttpGet("list")]
    [Authorize(Policy = "perm:api.monitor.view")]
    public async Task<IActionResult> List([FromQuery] int take = 50)
    {
        var items = await _testPlanService.ListAsync(take);
        var result = items.Select(p => new
        {
            p.Id,
            p.Name,
            p.Description,
            MonitorCount = p.MonitorIds.Count,
            p.CreatedAt,
            p.UpdatedAt
        });
        return Ok(result);
    }

    [HttpGet("{id}")]
    [Authorize(Policy = "perm:api.monitor.view")]
    public async Task<IActionResult> Get(Guid id)
    {
        var plan = await _testPlanService.GetByIdAsync(id);
        if (plan == null) return NotFound();
        return Ok(new
        {
            plan.Id,
            plan.Name,
            plan.Description,
            plan.MonitorIds,
            plan.CreatedAt,
            plan.UpdatedAt
        });
    }

    [HttpPost]
    [Authorize(Policy = "perm:api.monitor.create")]
    public async Task<IActionResult> Create([FromBody] TestPlanDto dto)
    {
        var id = await _testPlanService.AddAsync(dto);
        return Ok(id);
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "perm:api.monitor.update")]
    public async Task<IActionResult> Update(Guid id, [FromBody] TestPlanDto dto)
    {
        await _testPlanService.UpdateAsync(id, dto);
        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "perm:api.monitor.delete")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _testPlanService.DeleteAsync(id);
        return NoContent();
    }

    [HttpPost("{id}/run")]
    [Authorize(Policy = "perm:api.monitor.run")]
    public async Task<IActionResult> Run(Guid id)
    {
        var planRunId = await _testPlanService.ExecutePlanAsync(id);
        return Ok(new { planRunId });
    }

    [HttpGet("{id}/report")]
    [Authorize(Policy = "perm:api.monitor.view")]
    public async Task<IActionResult> GetReport(Guid id, [FromQuery] Guid planRunId)
    {
        var report = await _reportService.GenerateReportAsync(id, planRunId);
        return Ok(report);
    }

    [HttpGet("{id}/report/html")]
    [Authorize(Policy = "perm:api.monitor.view")]
    public async Task<IActionResult> GetHtmlReport(Guid id, [FromQuery] Guid planRunId)
    {
        var report = await _reportService.GenerateReportAsync(id, planRunId);
        var html = _reportService.GenerateHtmlReport(report);
        return Content(html, "text/html; charset=utf-8");
    }

    [HttpGet("{id}/runs")]
    [Authorize(Policy = "perm:api.monitor.view")]
    public async Task<IActionResult> GetRuns(Guid id, [FromQuery] int take = 20)
    {
        var runs = await _reportService.ListPlanRunsAsync(id, take);
        return Ok(runs);
    }
}

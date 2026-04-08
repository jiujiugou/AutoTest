using AutoTest.Application;
using AutoTest.Application.Dto;
using Microsoft.AspNetCore.Mvc;

namespace AutoTest.Webapi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MonitorController : ControllerBase
{
    private readonly IMonitorService _monitorService;
    private readonly IWorkflowScheduler _workflowScheduler;

    public MonitorController(IMonitorService monitorService, IWorkflowScheduler workflowScheduler)
    {
        _monitorService = monitorService;
        _workflowScheduler=workflowScheduler;
    }

    [HttpGet]
    public IActionResult Index() => Ok("Server running");

    [HttpGet("list")]
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
            AssertionCount = m.Assertions.Count
        });
        return Ok(result);
    }

    //根据ID查询
    [HttpGet("{id}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var result = await _monitorService.GetByIdAsync(id);
        if (result == null)
            return NotFound();

        return Ok(result);
    }

    //创建
    [HttpPost]
    public async Task<IActionResult> Add([FromBody] MonitorDto dto)
    {
        var id = await _monitorService.AddAsync(dto);
        return Ok(id);
    }

    //删除
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _monitorService.DeleteAsync(id);
        return NoContent();
    }
    //更新
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] MonitorDto dto)
    {
        await _monitorService.UpdateAsync(id, dto);
        return NoContent(); // 更新成功但不返回内容
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

    [HttpGet("executions/{executionId}/assertions")]
    public async Task<IActionResult> GetExecutionAssertions(Guid executionId)
    {
        var assertions = await _monitorService.GetExecutionAssertionResultsAsync(executionId);
        return Ok(assertions);
    }
    [HttpPost("{id}/run")]
    public async Task<IActionResult> TaskRun(Guid id)
    {
        await _workflowScheduler.RunNowAsync(id);
        return Ok();
    }
}

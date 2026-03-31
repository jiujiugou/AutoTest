using AutoTest.Application;
using AutoTest.Application.Dto;
using Microsoft.AspNetCore.Mvc;

namespace AutoTest.Webapi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MonitorController : ControllerBase
{
    private readonly IMonitorService _monitorService;

    public MonitorController(IMonitorService monitorService)
    {
        _monitorService = monitorService;
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
    public async Task<IActionResult> TaskRun(Guid id)
    {
        await _monitorService.TaskRunAsync(id);
        return Ok();
    }
}

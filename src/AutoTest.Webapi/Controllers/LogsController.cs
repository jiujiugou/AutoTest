using AutoTest.Application;
using AutoTest.Application.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AutoTest.Webapi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class LogsController : ControllerBase
{
    private readonly ILogService _logService;

    public LogsController(ILogService logService)
    {
        _logService = logService;
    }

    [HttpGet]
    [Authorize(Policy = "perm:logs.view")]
    public async Task<ActionResult<LogPageDto>> List(
        [FromQuery] int take = 100,
        [FromQuery] string? level = null,
        [FromQuery] string? module = null,
        [FromQuery] string? keyword = null,
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null,
        [FromQuery] string? before = null)
    {
        var query = new LogQueryDto(
            Take: take,
            Level: level,
            Module: module,
            Keyword: keyword,
            FromUtc: fromUtc,
            ToUtc: toUtc,
            Before: before);

        try
        {
            var page = await _logService.QueryAsync(query);
            return Ok(page);
        }
        catch (Exception ex)
        {
            return Problem(ex.Message);
        }
    }

    [HttpDelete]
    public async Task<IActionResult> Clear()
    {
        try
        {
            await _logService.ClearAsync();
        }
        catch
        {
            return Problem("清空日志失败");
        }

        return NoContent();
    }
}

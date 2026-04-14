using System.ComponentModel;
using System.Text.Json;
using AutoTest.Application;
using AutoTest.Application.Dto;
using Microsoft.SemanticKernel;

namespace AutoTest.Webapi.AI;

public class AutoTestPlugin
{
    private readonly IMonitorService _monitorService;
    private readonly IWorkflowScheduler _workflowScheduler;

    public AutoTestPlugin(
        IMonitorService monitorService,
        IWorkflowScheduler workflowScheduler)
    {
        _monitorService = monitorService;
        _workflowScheduler = workflowScheduler;
    }

    // =========================
    // 1. 获取监控列表（结构化版本）
    // =========================
    [KernelFunction("monitor_list")]
    [Description("当用户询问系统中有哪些监控任务时使用。返回监控任务列表（JSON数组）。")]
    public async Task<string> GetMonitorListAsync()
    {
        var items = await _monitorService.ListAsync(50);

        var result = items.Select(m => new
        {
            m.Id,
            m.Name,
            TargetType = m.Target.Type,
            m.IsEnabled,
            Status = (int)m.Status,
            m.LastRunTime,
            m.AutoDailyEnabled,
            m.AutoDailyTime,
            m.MaxRuns,
            m.ExecutedCount
        }).ToList();
        
        return JsonSerializer.Serialize(result);
    }

    // =========================
    // 2. 执行监控任务（更安全版本）
    // =========================
    [KernelFunction("monitor_run")]
    [Description("当用户要求立即执行某个监控任务时使用。需要监控任务ID。")]
    public async Task<string> RunMonitorAsync(
        [Description("监控任务ID（GUID字符串）")] string monitorId)
    {
        if (!Guid.TryParse(monitorId, out var id))
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = "INVALID_ID",
                message = "monitorId格式错误，应为GUID"
            });
        }

        try
        {
            await _workflowScheduler.RunNowAsync(id, "ai-agent");

            return JsonSerializer.Serialize(new
            {
                success = true,
                monitorId = id,
                message = "任务已触发执行"
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = "EXEC_FAILED",
                message = ex.Message
            });
        }
    }

    // =========================
    // 3. 获取统计信息（稳定结构）
    // =========================
    [KernelFunction("monitor_stats")]
    [Description("当用户询问某个监控任务的运行情况、成功率、失败次数时使用。")]
    public async Task<string> GetStatsAsync(
        [Description("监控任务ID（GUID字符串）")] string monitorId)
    {
        if (!Guid.TryParse(monitorId, out var id))
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = "INVALID_ID",
                message = "monitorId格式错误"
            });
        }

        var (stats, _) = await _monitorService.GetMonitorRuntimeStatsAsync(id, 0);

        return JsonSerializer.Serialize(new
        {
            success = true,
            data = stats
        });
    }

}

using System;

namespace AutoTest.Application
{
    /// <summary>
    /// 工作流/任务调度器接口。
    /// </summary>
    /// <remarks>
    /// 负责将监控任务加入后台调度系统（如 Hangfire）并支持立即执行、延迟执行与每日定时。
    /// </remarks>
    public interface IWorkflowScheduler
    {
        /// <summary>
        /// 立即触发执行指定工作流/监控任务。
        /// </summary>
        Task RunNowAsync(Guid workflowId);

        /// <summary>
        /// 立即触发执行，并记录触发者（可用于审计）。
        /// </summary>
        Task RunNowAsync(Guid workflowId, string? userId);

        /// <summary>
        /// 延迟执行指定工作流/监控任务。
        /// </summary>
        Task RunAfterAsync(Guid workflowId, TimeSpan delay);

        /// <summary>
        /// 创建或更新每日定时执行计划。
        /// </summary>
        Task UpsertDailyMonitorAsync(Guid monitorId, string timeHHmm);

        /// <summary>
        /// 移除指定监控任务的定时计划。
        /// </summary>
        Task RemoveMonitorScheduleAsync(Guid monitorId);
    }

}

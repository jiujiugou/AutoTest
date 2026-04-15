using AutoTest.Application;
using AutoTest.Infrastructure;
using Hangfire;
using Hangfire.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace AutoTest.Infrastructure
{
    /// <summary>
    /// Hangfire 调度实现：将监控任务投递到 Hangfire 的即时/延迟/周期队列中执行。
    /// </summary>
    public class HangfireWorkflowScheduler : IWorkflowScheduler
    {
        private static string MonitorJobId(Guid monitorId) => $"monitor:{monitorId}";

        /// <summary>
        /// 延迟执行一次任务。
        /// </summary>
        /// <param name="workflowId">监控任务 ID。</param>
        /// <param name="delay">延迟时间。</param>
        public Task RunAfterAsync(Guid workflowId, TimeSpan delay)
        {
            BackgroundJob.Schedule<WorkflowJob>(job => job.RunAsync(workflowId), delay);
            return Task.CompletedTask;
        }


        /// <summary>
        /// 立即执行一次任务（系统触发）。
        /// </summary>
        /// <param name="workflowId">监控任务 ID。</param>
        public Task RunNowAsync(Guid workflowId)
        {
            BackgroundJob.Enqueue<WorkflowJob>(job => job.RunAsync(workflowId));
            return Task.CompletedTask;
        }

        /// <summary>
        /// 立即执行一次任务（用户触发），用于将实时通知定向推送到该用户。
        /// </summary>
        /// <param name="workflowId">监控任务 ID。</param>
        /// <param name="userId">触发用户 ID。</param>
        public Task RunNowAsync(Guid workflowId, string? userId)
        {
            BackgroundJob.Enqueue<WorkflowJob>(job => job.RunAsync(workflowId, userId));
            return Task.CompletedTask;
        }

        public Task RunNowAsync(Guid workflowId, string? userId, string? idempotencyKey)
        {
            BackgroundJob.Enqueue<WorkflowJob>(job => job.RunAsync(workflowId, userId, idempotencyKey));
            return Task.CompletedTask;
        }

        /// <summary>
        /// 添加或更新每日定时任务。
        /// </summary>
        /// <param name="monitorId">监控任务 ID。</param>
        /// <param name="timeHHmm">执行时间（HH:mm）。</param>
        public Task UpsertDailyMonitorAsync(Guid monitorId, string timeHHmm)
        {
            var (hour, minute) = ParseHHmm(timeHHmm);
            RecurringJob.AddOrUpdate<WorkflowJob>(
                MonitorJobId(monitorId),
                job => job.RunAsync(monitorId),
                Cron.Daily(hour, minute));
            return Task.CompletedTask;
        }

        /// <summary>
        /// 移除某个监控的周期调度。
        /// </summary>
        /// <param name="monitorId">监控任务 ID。</param>
        public Task RemoveMonitorScheduleAsync(Guid monitorId)
        {
            RecurringJob.RemoveIfExists(MonitorJobId(monitorId));
            return Task.CompletedTask;
        }

        private static (int hour, int minute) ParseHHmm(string timeHHmm)
        {
            var s = (timeHHmm ?? "").Trim();
            var parts = s.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length != 2)
                throw new ArgumentException("Invalid time format, expected HH:mm");

            if (!int.TryParse(parts[0], out var hour) || !int.TryParse(parts[1], out var minute))
                throw new ArgumentException("Invalid time format, expected HH:mm");

            if (hour < 0 || hour > 23 || minute < 0 || minute > 59)
                throw new ArgumentException("Invalid time range, expected HH:mm");

            return (hour, minute);
        }
    }
}

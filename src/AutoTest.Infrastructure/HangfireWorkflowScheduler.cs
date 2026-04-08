using AutoTest.Application;
using AutoTest.Infrastructure;
using Hangfire;
using Hangfire.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace AutoTest.Infrastructure
{
    public class HangfireWorkflowScheduler : IWorkflowScheduler
    {
        public Task RunAfterAsync(Guid workflowId, TimeSpan delay)
        {
            BackgroundJob.Schedule<WorkflowJob>(job => job.RunAsync(workflowId), delay);
            return Task.CompletedTask;
        }


        public Task RunNowAsync(Guid workflowId)
        {
            BackgroundJob.Enqueue<WorkflowJob>(job => job.RunAsync(workflowId));
            return Task.CompletedTask;
        }

        public Task ScheduleAsync(string workflowId, string cron="Daily")
        {
            RecurringJob.AddOrUpdate<WorkflowJob>(workflowId, job => job.ScanAndEnqueueAsync(), ToCron(cron));
            return Task.CompletedTask;
        }

        private static string ToCron(string cron)
        {
            switch (cron.ToLowerInvariant())
            {
                case "hourly": return Cron.Hourly();
                case "daily": return Cron.Daily();
                case "weekly": return Cron.Weekly();
                case "monthly": return Cron.Monthly();
                default:
                    throw new ArgumentException($"Unsupported cron expression: {cron}");
            }
        }
    }
}
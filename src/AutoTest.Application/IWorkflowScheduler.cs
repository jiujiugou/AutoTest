using System;

namespace AutoTest.Application
{
    public interface IWorkflowScheduler
    {
        Task RunNowAsync(Guid workflowId);
        Task RunAfterAsync(Guid workflowId, TimeSpan delay);
        Task ScheduleAsync(string jobId,string cron);
    }

}

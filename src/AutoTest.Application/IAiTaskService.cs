using AutoTest.Core.AI;
using System;
using System.Collections.Generic;
using System.Text;

namespace AutoTest.Application
{
    public interface IAiTaskService
    {
        /// <summary>
        /// 投递 AI 任务（进入队列/数据库）
        /// </summary>
        Task EnqueueAsync(AiTask aiTask, CancellationToken cancellationToken = default);

        /// <summary>
        /// 拉取一批待处理任务（用于 Worker 消费）
        /// </summary>
        Task<List<AiTask>> TakeBatchAsync(int batchSize, CancellationToken cancellationToken = default);

        /// <summary>
        /// 标记任务成功完成
        /// </summary>
        Task MarkCompletedAsync(Guid taskId, string resultJson, CancellationToken cancellationToken = default);

        /// <summary>
        /// 标记任务失败（进入重试机制）
        /// </summary>
        Task MarkFailedAsync(Guid taskId, string error, DateTime nextRunAt, CancellationToken cancellationToken = default);

        /// <summary>
        /// 可选：查询任务状态
        /// </summary>
        //Task<AiTask?> GetAsync(Guid taskId, CancellationToken cancellationToken = default);
    }
}

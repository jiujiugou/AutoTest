using AutoTest.Core.AI;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AutoTest.Core.Repositories
{
    public interface IAnalysisRepository
    {
        Task AddAsync(AIAnalysis analysis);

        /// <summary>
        /// 按执行记录 ID 获取 AI 分析结果
        /// </summary>
        Task<AIAnalysis?> GetByExecutionRecordIdAsync(Guid executionRecordId);

        /// <summary>
        /// 按监控任务 ID 获取分析结果列表（最近 N 条）
        /// </summary>
        Task<List<AIAnalysis>> GetByMonitorIdAsync(Guid monitorId, int take = 20);
    }
}

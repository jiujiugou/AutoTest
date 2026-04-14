using System.Data;
namespace AutoTest.Core.Abstraction;

/// <summary>
/// 监控任务仓储接口。
/// </summary>
/// <remarks>
/// 用于对 <see cref="MonitorEntity"/> 进行持久化读写，并支持在同一事务中执行写操作。
/// </remarks>
public interface IMonitorRepository
{
    /// <summary>
    /// 根据 ID 获取监控任务。
    /// </summary>
    Task<MonitorEntity?> GetByIdAsync(Guid id, IDbTransaction? tx = null);

    /// <summary>
    /// 新增监控任务。
    /// </summary>
    Task AddAsync(MonitorEntity monitor, IDbTransaction? tx = null);

    /// <summary>
    /// 更新监控任务。
    /// </summary>
    Task UpdateAsync(MonitorEntity monitor, IDbTransaction? tx = null);

    /// <summary>
    /// 删除监控任务。
    /// </summary>
    Task RemoveAsync(Guid id, IDbTransaction? tx = null);

    /// <summary>
    /// 获取待执行的任务集合（通常用于调度器）。
    /// </summary>
    Task<IEnumerable<MonitorEntity>> GetPendingTasksAsync();

    /// <summary>
    /// 获取监控任务列表。
    /// </summary>
    Task<IEnumerable<MonitorEntity>> ListAsync(int take = 50);

}

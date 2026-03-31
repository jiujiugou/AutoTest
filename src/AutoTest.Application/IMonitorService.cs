using AutoTest.Application.Dto;
using AutoTest.Core;

namespace AutoTest.Application;

public interface IMonitorService
{
    // 创建一个新的监控
    Task<Guid> AddAsync(MonitorDto dto);

    // 更新已有监控
    Task UpdateAsync(Guid id, MonitorDto dto);

    // 删除监控
    Task DeleteAsync(Guid id);

    // 根据 Id 获取单个监控（业务用）
    Task<MonitorEntity?> GetByIdAsync(Guid id);
    //任务运行
    Task TaskRunAsync(Guid id, CancellationToken cancellationToken = default);
    // 获取所有待执行的监控任务（供后台服务调用）
    Task<IEnumerable<MonitorEntity>> GetPendingTasksAsync();
}

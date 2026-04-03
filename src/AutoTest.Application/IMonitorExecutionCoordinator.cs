namespace AutoTest.Application;

/// <summary>
/// 避免同一 Monitor 在队列中重复并发执行（API 触发与后台调度共用）。
/// </summary>
public interface IMonitorExecutionCoordinator
{
    /// <returns>若已为该 Monitor 占用则返回 false，不应再入队。</returns>
    bool TryBegin(Guid monitorId);

    void End(Guid monitorId);
}

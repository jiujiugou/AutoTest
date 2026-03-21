namespace AutoTest.Core.Abstraction;

public interface IMonitorRepository
{
    Task<MonitorEntity?> GetByIdAsync(Guid id);
    Task AddAsync(MonitorEntity monitor);
    Task UpdateAsync(MonitorEntity monitor);
    Task RemoveAsync(Guid id);
}

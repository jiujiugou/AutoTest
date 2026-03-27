using System.Data;
namespace AutoTest.Core.Abstraction;

public interface IMonitorRepository
{
    Task<MonitorEntity?> GetByIdAsync(Guid id, IDbTransaction? tx = null);

    Task AddAsync(MonitorEntity monitor, IDbTransaction? tx = null);

    Task UpdateAsync(MonitorEntity monitor, IDbTransaction? tx = null);

    Task RemoveAsync(Guid id, IDbTransaction? tx = null);
}

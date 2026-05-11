using System.Data;
using AutoTest.Core;

namespace AutoTest.Core.Repositories;

public interface ITestPlanRepository
{
    Task<TestPlanEntity?> GetByIdAsync(Guid id, IDbTransaction? tx = null);
    Task<IEnumerable<TestPlanEntity>> ListAsync(int take = 50);
    Task AddAsync(TestPlanEntity plan, IDbTransaction? tx = null);
    Task UpdateAsync(TestPlanEntity plan, IDbTransaction? tx = null);
    Task RemoveAsync(Guid id, IDbTransaction? tx = null);
}

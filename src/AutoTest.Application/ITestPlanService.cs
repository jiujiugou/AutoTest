using AutoTest.Application.Dto;
using AutoTest.Core;

namespace AutoTest.Application;

public interface ITestPlanService
{
    Task<Guid> AddAsync(TestPlanDto dto);
    Task UpdateAsync(Guid id, TestPlanDto dto);
    Task DeleteAsync(Guid id);
    Task<TestPlanEntity?> GetByIdAsync(Guid id);
    Task<IEnumerable<TestPlanEntity>> ListAsync(int take = 50);
    Task<Guid> ExecutePlanAsync(Guid planId, string? lockedBy = null);
}

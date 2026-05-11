using AutoTest.Application.Dto;
using AutoTest.Core;
using AutoTest.Core.Execution;
using AutoTest.Core.Repositories;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application;

public class TestPlanService : ITestPlanService
{
    private readonly ITestPlanRepository _repository;
    private readonly IMonitorService _monitorService;
    private readonly IOrchestrator _orchestrator;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<TestPlanService> _logger;

    public TestPlanService(
        ITestPlanRepository repository,
        IMonitorService monitorService,
        IOrchestrator orchestrator,
        IUnitOfWork unitOfWork,
        ILogger<TestPlanService> logger)
    {
        _repository = repository;
        _monitorService = monitorService;
        _orchestrator = orchestrator;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Guid> AddAsync(TestPlanDto dto)
    {
        var now = DateTime.UtcNow;
        var entity = new TestPlanEntity(
            Guid.NewGuid(),
            dto.Name,
            dto.Description,
            dto.MonitorIds,
            now,
            now);

        await _unitOfWork.ExecuteAsync(async tx =>
        {
            await _repository.AddAsync(entity, tx);
        });

        _logger.LogInformation("TestPlan {Id} created", entity.Id);
        return entity.Id;
    }

    public async Task UpdateAsync(Guid id, TestPlanDto dto)
    {
        await _unitOfWork.ExecuteAsync(async tx =>
        {
            var existing = await _repository.GetByIdAsync(id, tx)
                ?? throw new InvalidOperationException("TestPlan not found");

            existing.Update(dto.Name, dto.Description, dto.MonitorIds);
            await _repository.UpdateAsync(existing, tx);
        });

        _logger.LogInformation("TestPlan {Id} updated", id);
    }

    public async Task DeleteAsync(Guid id)
    {
        await _unitOfWork.ExecuteAsync(async tx =>
        {
            await _repository.RemoveAsync(id, tx);
        });
        _logger.LogInformation("TestPlan {Id} deleted", id);
    }

    public Task<TestPlanEntity?> GetByIdAsync(Guid id) =>
        _repository.GetByIdAsync(id);

    public Task<IEnumerable<TestPlanEntity>> ListAsync(int take = 50) =>
        _repository.ListAsync(take);

    /// <summary>
    /// 执行计划内所有监控，同一批次共享一个 PlanRunId。
    /// </summary>
    private static readonly SemaphoreSlim _planConcurrencyLimiter = new(
        Math.Max(1, Environment.ProcessorCount));

    public async Task<Guid> ExecutePlanAsync(Guid planId, string? lockedBy = null)
    {
        var plan = await _repository.GetByIdAsync(planId)
            ?? throw new InvalidOperationException("TestPlan not found");

        var planRunId = Guid.NewGuid();
        lockedBy ??= $"plan-executor:{planRunId.ToString("N")[..8]}";

        _logger.LogInformation("Executing TestPlan {PlanId}, PlanRunId {PlanRunId}, {Count} monitors",
            planId, planRunId, plan.MonitorIds.Count);

        var tasks = plan.MonitorIds.Select(monitorId => Task.Run(async () =>
        {
            var idempotencyKey = $"plan:{planRunId:N}:{monitorId:N}";
            await _planConcurrencyLimiter.WaitAsync();
            try
            {
                var (started, executionId, startedAtUtc) =
                    await _monitorService.TryStartExecutionAsync(monitorId, idempotencyKey, lockedBy, planRunId);

                if (!started)
                {
                    _logger.LogWarning("Monitor {MonitorId} skipped (duplicate)", monitorId);
                    return;
                }

                var monitor = await _monitorService.GetByIdAsync(monitorId);
                if (monitor == null)
                {
                    _logger.LogWarning("Monitor {MonitorId} not found during plan execution", monitorId);
                    return;
                }

                await _orchestrator.TryExecuteAsync(monitor, executionId, startedAtUtc, lockedBy);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Monitor {MonitorId} failed in plan {PlanRunId}", monitorId, planRunId);
            }
            finally
            {
                _planConcurrencyLimiter.Release();
            }
        }));

        await Task.WhenAll(tasks);

        _logger.LogInformation("TestPlan {PlanId} execution complete, PlanRunId {PlanRunId}", planId, planRunId);
        return planRunId;
    }
}

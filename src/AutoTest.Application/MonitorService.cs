using AutoTest.Application;
using AutoTest.Application.Builder;
using AutoTest.Application.Dto;
using AutoTest.Core;
using AutoTest.Core.Abstraction;
using AutoTest.Core.Assertion;
using AutoTest.Core.Execution;
using CacheCommons;
using Microsoft.Extensions.Logging; // 假设你的 ILogger 在这里
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// 监控任务应用服务实现。
/// </summary>
/// <remarks>
/// 负责将 DTO/配置映射为领域模型，并通过仓储与工作单元完成持久化。
/// 执行相关的编排由 <see cref="IOrchestrator"/> 负责。
/// </remarks>
public class MonitorService : IMonitorService
{
    private readonly IMonitorRepository _monitorRepository;
    private readonly ICacheService _cacheService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IOrchestrator _orchestrator;
    private readonly IExecutionRecordRepository _executionRecordRepository;
    private readonly IEnumerable<IAssertionRuleMap> _assertionBuilders;
    private readonly IEnumerable<ITargetMap> _targetBuilders;
    private readonly ILogger<MonitorService> _logger;

    public MonitorService(
        IEnumerable<ITargetMap> targetBuilders,
        IEnumerable<IAssertionRuleMap> assertionRuleBuilder,
        IMonitorRepository monitorRepository,
        ICacheService cacheService,
        IUnitOfWork unitOfWork,
        IOrchestrator orchestrator,
        IExecutionRecordRepository executionRecordRepository,
        ILogger<MonitorService> logger)
    {
        _monitorRepository = monitorRepository;
        _cacheService = cacheService;
        _unitOfWork = unitOfWork;
        _assertionBuilders = assertionRuleBuilder;
        _targetBuilders = targetBuilders;
        _orchestrator = orchestrator;
        _executionRecordRepository = executionRecordRepository;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Guid> AddAsync(MonitorDto dto)
    {
        _logger.LogInformation("AddAsync started for monitor: {Name}", dto.Name);
        try
        {
            var targetBuilder = _targetBuilders.FirstOrDefault(b => string.Equals(b.Type, dto.TargetType, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException($"No target builder found for type: {dto.TargetType}");

            var target = targetBuilder.Map(dto.TargetConfig);

            var assertions = dto.Assertions
                .Select(aDto =>
                {
                    var builder = _assertionBuilders.FirstOrDefault(b => string.Equals(b.Type, aDto.Type, StringComparison.OrdinalIgnoreCase))
                        ?? throw new InvalidOperationException($"Unknown assertion type: {aDto.Type}");
                    return builder.Map(aDto.Id, aDto.ConfigJson);
                })
                .ToList();

            var monitorEntity = new MonitorEntity(
                Guid.NewGuid(),
                dto.Name,
                target,
                MonitorStatus.Pending,
                null,
                dto.IsEnabled,
                dto.AutoDailyEnabled,
                dto.AutoDailyTime,
                dto.MaxRuns,
                dto.ExecutedCount);
            foreach (var assertion in assertions)
                monitorEntity.AddAssertion(assertion);
            await _unitOfWork.ExecuteAsync(async tx =>
            {
                await _monitorRepository.AddAsync(monitorEntity, tx);
            });
            _logger.LogInformation("Monitor {Id} added successfully", monitorEntity.Id);
            return monitorEntity.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AddAsync failed for monitor: {Name}", dto.Name);
            await _unitOfWork.RollbackAsync();
            throw;
        }
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid id)
    {
        _logger.LogInformation("DeleteAsync started for monitor: {Id}", id);
        try
        {
            await _unitOfWork.ExecuteAsync(async tx =>
            {
                var existing = await _monitorRepository.GetByIdAsync(id, tx)
                               ?? throw new InvalidOperationException("Monitor not found");
                await _monitorRepository.RemoveAsync(existing.Id, tx);
            });
            _logger.LogInformation("Monitor {Id} deleted successfully", id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DeleteAsync failed for monitor: {Id}", id);
            await _unitOfWork.RollbackAsync();
            throw;
        }

        await _cacheService.RemoveAsync($"Monitor{id}");
    }

    /// <inheritdoc />
    public async Task<MonitorEntity?> GetByIdAsync(Guid id)
    {
        _logger.LogInformation("GetByIdAsync called for monitor: {Id}", id);
        var cacheKey = $"Monitor{id}";
        var cached = await _cacheService.GetOrCreateAsync<MonitorEntity>(cacheKey, async () =>
        {
            _logger.LogInformation("Cache miss for monitor: {Id}", id);
            return await _monitorRepository.GetByIdAsync(id);
        });
        return cached;
    }

    /// <inheritdoc />
    public async Task UpdateAsync(Guid id, MonitorDto dto)
    {
        _logger.LogInformation("UpdateAsync started for monitor: {Id}", id);
        try
        {
            await _unitOfWork.ExecuteAsync(async tx =>
            {
                var existing = await _monitorRepository.GetByIdAsync(id,tx)
                           ?? throw new InvalidOperationException("Monitor not found");

                var targetBuilder = _targetBuilders.FirstOrDefault(b => b.Type == dto.TargetType)
                                    ?? throw new InvalidOperationException($"No target builder for type: {dto.TargetType}");

                existing.Update(dto.Name, targetBuilder.Map(dto.TargetConfig), dto.IsEnabled);
                var autoDailyEnabled = dto.AutoDailyEnabled;
                if (dto.MaxRuns != null && existing.ExecutedCount >= dto.MaxRuns.Value)
                    autoDailyEnabled = false;
                existing.UpdateSchedule(autoDailyEnabled, dto.AutoDailyTime, dto.MaxRuns);
                existing.ClearAssertions();
                foreach (var aDto in dto.Assertions)
                {
                    var builder = _assertionBuilders.FirstOrDefault(b => b.Type == aDto.Type)
                                  ?? throw new InvalidOperationException($"Unknown assertion type: {aDto.Type}");
                    existing.AddAssertion(builder.Map(aDto.Id, aDto.ConfigJson));
                }
            
                await _monitorRepository.UpdateAsync(existing, tx);
            });
            _logger.LogInformation("Monitor {Id} updated successfully", id);

            await _cacheService.RemoveAsync($"Monitor{id}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UpdateAsync failed for monitor: {Id}", id);
            throw;
        }
    }

    /// <inheritdoc />
    public Task<IEnumerable<MonitorEntity>> GetPendingTasksAsync() =>
        _monitorRepository.GetPendingTasksAsync();

    /// <inheritdoc />
    public Task<IEnumerable<MonitorEntity>> ListAsync(int take = 50) =>
        _monitorRepository.ListAsync(take);

    /// <inheritdoc />
    public Task<ExecutionRecord?> GetLatestExecutionAsync(Guid monitorId)
    {
        return _executionRecordRepository.GetLatestByMonitorIdAsync(monitorId);
    }

    /// <inheritdoc />
    public Task<IEnumerable<ExecutionRecord>> GetExecutionsAsync(Guid monitorId, int take = 20)
    {
        return _executionRecordRepository.GetByMonitorIdAsync(monitorId, take);
    }

    /// <inheritdoc />
    public Task<IEnumerable<AssertionResult>> GetExecutionAssertionResultsAsync(Guid executionId)
    {
        return _executionRecordRepository.GetAssertionResultsAsync(executionId);
    }

    /// <inheritdoc />
    public async Task SetEnabledAsync(Guid id, bool isEnabled)
    {
        await _unitOfWork.ExecuteAsync(async tx =>
        {
            var existing = await _monitorRepository.GetByIdAsync(id, tx)
                           ?? throw new InvalidOperationException("Monitor not found");

            existing.Update(existing.Name, existing.Target, isEnabled);
            await _monitorRepository.UpdateAsync(existing, tx);
        });

        await _cacheService.RemoveAsync($"Monitor{id}");
    }

    /// <inheritdoc />
    public async Task SetScheduleAsync(Guid id, bool autoDailyEnabled, string? autoDailyTime, int? maxRuns)
    {
        await _unitOfWork.ExecuteAsync(async tx =>
        {
            var existing = await _monitorRepository.GetByIdAsync(id, tx)
                           ?? throw new InvalidOperationException("Monitor not found");

            if (maxRuns != null && existing.ExecutedCount >= maxRuns.Value)
                autoDailyEnabled = false;
            existing.UpdateSchedule(autoDailyEnabled, autoDailyTime, maxRuns);
            await _monitorRepository.UpdateAsync(existing, tx);
        });

        await _cacheService.RemoveAsync($"Monitor{id}");
    }

    /// <inheritdoc />
    public async Task<(bool AutoDailyEnabled, string? AutoDailyTime, int? MaxRuns, int ExecutedCount)> GetScheduleAsync(Guid id)
    {
        var m = await GetByIdAsync(id);
        if (m == null) 
            throw new InvalidOperationException("Monitor not found");
        return (m.AutoDailyEnabled, m.AutoDailyTime, m.MaxRuns, m.ExecutedCount);
    }

    /// <inheritdoc />
    public async Task<bool> IncrementAutoExecutedCountAndDisableIfReachedAsync(Guid id)
    {
        var shouldDisable = false;

        await _unitOfWork.ExecuteAsync(async tx =>
        {
            var existing = await _monitorRepository.GetByIdAsync(id, tx);
            if (existing == null)
                return;

            if (!existing.AutoDailyEnabled)
                return;

            existing.SetExecutedCount(existing.ExecutedCount + 1);

            if (existing.MaxRuns != null && existing.ExecutedCount >= existing.MaxRuns.Value)
            {
                existing.UpdateSchedule(false, existing.AutoDailyTime, existing.MaxRuns);
                shouldDisable = true;
            }

            await _monitorRepository.UpdateAsync(existing, tx);
        });

        await _cacheService.RemoveAsync($"Monitor{id}");
        return shouldDisable;
    }

    /// <inheritdoc />
    public async Task<(MonitorExecutionStats Stats, IEnumerable<MonitorErrorStat> TopErrors)> GetMonitorRuntimeStatsAsync(Guid monitorId, int takeTopErrors = 10)
    {
        var stats = await _executionRecordRepository.GetMonitorExecutionStatsAsync(monitorId);
        var topErrors = await _executionRecordRepository.GetTopErrorStatsAsync(monitorId, takeTopErrors);
        return (stats, topErrors);
    }
}

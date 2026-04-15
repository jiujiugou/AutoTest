using AutoTest.Application;
using AutoTest.Application.Builder;
using AutoTest.Application.Dto;
using AutoTest.Core;
using AutoTest.Core.Abstraction;
using AutoTest.Core.Assertion;
using AutoTest.Core.Execution;
using CacheCommons;
using Microsoft.Extensions.Logging;
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

    /// <summary>
    /// 初始化 <see cref="MonitorService"/>。
    /// </summary>
    /// <param name="targetBuilders">目标配置映射器集合（按 TargetType 选择）。</param>
    /// <param name="assertionRuleBuilder">断言规则映射器集合（按 AssertionType 选择）。</param>
    /// <param name="monitorRepository">监控任务仓储。</param>
    /// <param name="cacheService">缓存服务（用于热点监控任务缓存）。</param>
    /// <param name="unitOfWork">工作单元（事务封装）。</param>
    /// <param name="orchestrator">执行编排器（执行/断言/落库/通知）。</param>
    /// <param name="executionRecordRepository">执行记录仓储。</param>
    /// <param name="logger">日志记录器。</param>
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

    /// <summary>
    /// 创建监控任务：将 DTO 中的 TargetConfig/Assertions 映射为领域对象并持久化。
    /// </summary>
    /// <param name="dto">监控任务 DTO。</param>
    /// <returns>新建监控任务 ID。</returns>
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

    /// <summary>
    /// 删除监控任务（包含断言规则），并清理相关缓存。
    /// </summary>
    /// <param name="id">监控任务 ID。</param>
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

    /// <summary>
    /// 按 ID 获取监控任务（优先从缓存读取）。
    /// </summary>
    /// <param name="id">监控任务 ID。</param>
    /// <returns>监控任务实体；不存在则返回 null。</returns>
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

    /// <summary>
    /// 更新监控任务：更新基础信息/目标配置/调度配置，并重建断言规则；同时清理缓存。
    /// </summary>
    /// <param name="id">监控任务 ID。</param>
    /// <param name="dto">更新 DTO。</param>
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


    /// <summary>
    /// 获取监控任务列表。
    /// </summary>
    /// <param name="take">最多返回条数。</param>
    /// <returns>监控任务集合。</returns>
    public Task<IEnumerable<MonitorEntity>> ListAsync(int take = 50) =>
        _monitorRepository.ListAsync(take);

    /// <summary>
    /// 获取某个监控的最新一次执行记录。
    /// </summary>
    /// <param name="monitorId">监控任务 ID。</param>
    /// <returns>最新执行记录；不存在则返回 null。</returns>
    public Task<ExecutionRecord?> GetLatestExecutionAsync(Guid monitorId)
    {
        return _executionRecordRepository.GetLatestByMonitorIdAsync(monitorId);
    }

    /// <summary>
    /// 获取某个监控的执行记录列表（按开始时间倒序）。
    /// </summary>
    /// <param name="monitorId">监控任务 ID。</param>
    /// <param name="take">最多返回条数。</param>
    /// <returns>执行记录集合。</returns>
    public Task<IEnumerable<ExecutionRecord>> GetExecutionsAsync(Guid monitorId, int take = 20)
    {
        return _executionRecordRepository.GetByMonitorIdAsync(monitorId, take);
    }

    /// <summary>
    /// 获取某次执行对应的断言结果列表。
    /// </summary>
    /// <param name="executionId">执行记录 ID。</param>
    /// <returns>断言结果集合。</returns>
    public Task<IEnumerable<AssertionResult>> GetExecutionAssertionResultsAsync(Guid executionId)
    {
        return _executionRecordRepository.GetAssertionResultsAsync(executionId);
    }

    /// <summary>
    /// 启用或禁用监控任务，并清理缓存。
    /// </summary>
    /// <param name="id">监控任务 ID。</param>
    /// <param name="isEnabled">是否启用。</param>
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

    /// <summary>
    /// 设置自动调度参数（每日执行开关/时间/最大次数），并清理缓存。
    /// </summary>
    /// <param name="id">监控任务 ID。</param>
    /// <param name="autoDailyEnabled">是否启用每日自动执行。</param>
    /// <param name="autoDailyTime">每日执行时间（HH:mm）。</param>
    /// <param name="maxRuns">最大自动执行次数（为空表示不限制）。</param>
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

    /// <summary>
    /// 获取监控任务的调度参数（自动执行开关/时间/最大次数/已执行次数）。
    /// </summary>
    /// <param name="id">监控任务 ID。</param>
    /// <returns>调度参数。</returns>
    public async Task<(bool AutoDailyEnabled, string? AutoDailyTime, int? MaxRuns, int ExecutedCount)> GetScheduleAsync(Guid id)
    {
        var m = await GetByIdAsync(id);
        if (m == null) 
            throw new InvalidOperationException("Monitor not found");
        return (m.AutoDailyEnabled, m.AutoDailyTime, m.MaxRuns, m.ExecutedCount);
    }

    /// <summary>
    /// 自增自动执行次数；若达到最大次数则自动关闭自动调度。
    /// </summary>
    /// <param name="id">监控任务 ID。</param>
    /// <returns>若达到上限并关闭了自动调度则返回 true，否则返回 false。</returns>
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

    /// <summary>
    /// 获取某个监控的运行统计（执行总数/成功/失败/最近）以及失败原因 TopN。
    /// </summary>
    /// <param name="monitorId">监控任务 ID。</param>
    /// <param name="takeTopErrors">Top 错误条数。</param>
    /// <returns>运行统计与 Top 错误。</returns>
    public async Task<(MonitorExecutionStats Stats, IEnumerable<MonitorErrorStat> TopErrors)> GetMonitorRuntimeStatsAsync(Guid monitorId, int takeTopErrors = 10)
    {
        var stats = await _executionRecordRepository.GetMonitorExecutionStatsAsync(monitorId);
        var topErrors = await _executionRecordRepository.GetTopErrorStatsAsync(monitorId, takeTopErrors);
        return (stats, topErrors);
    }

    public async Task<(bool Started, Guid ExecutionId, DateTime StartedAtUtc)> TryStartExecutionAsync(Guid monitorId, string? idempotencyKey, string lockedBy)
    {
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var existingId = await _executionRecordRepository.GetIdByIdempotencyKeyAsync(idempotencyKey);
            if (existingId != null)
                return (false, existingId.Value, default);
        }

        var executionId = Guid.NewGuid();
        var startedAtUtc = DateTime.UtcNow;
        try
        {
            await _unitOfWork.ExecuteAsync(async tx =>
            {
                var monitor = await _monitorRepository.GetByIdAsync(monitorId, tx)
                              ?? throw new InvalidOperationException("Monitor not found");

                monitor.MarkRunning();
                await _monitorRepository.UpdateAsync(monitor, tx);

                var inserted = await _executionRecordRepository.TryAddRunningAsync(
                    executionId,
                    monitorId,
                    startedAtUtc,
                    idempotencyKey,
                    lockedBy,
                    heartbeatAtUtc: startedAtUtc,
                    tx);

                if (!inserted)
                    throw new DuplicateExecutionException(idempotencyKey!);
            });

            return (true, executionId, startedAtUtc);
        }
        catch (DuplicateExecutionException)
        {
            var existingId = await _executionRecordRepository.GetIdByIdempotencyKeyAsync(idempotencyKey!);
            if (existingId != null)
                return (false, existingId.Value, default);
            throw;
        }
    }

    private sealed class DuplicateExecutionException : Exception
    {
        public DuplicateExecutionException(string idempotencyKey)
            : base(idempotencyKey)
        {
        }
    }
}

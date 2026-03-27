using AutoTest.Application.Dto;
using AutoTest.Core;
using AutoTest.Core.Abstraction;
using AutoTest.Core.Assertion;
using CacheCommons;
namespace AutoTest.Application;

public class MonitorService : IMonitorService
{
    private readonly IMonitorRepository _monitorRepository;
    private readonly ICacheService _cacheService;
    private readonly IUnitOfWork _unitOfWork;

    // 注入你所有 Builder
    private readonly IEnumerable<IAssertionBuilder> _assertionBuilders;
    private readonly IEnumerable<ITargetBuilder> _targetBuilders;

    public MonitorService(
        IMonitorRepository monitorRepository,
        ICacheService cacheService,
        IUnitOfWork unitOfWork,
        IEnumerable<IAssertionBuilder> assertionBuilders,
        IEnumerable<ITargetBuilder> targetBuilders)
    {
        _monitorRepository = monitorRepository;
        _cacheService = cacheService;
        _unitOfWork = unitOfWork;
        _assertionBuilders = assertionBuilders;
        _targetBuilders = targetBuilders;
    }

    public async Task<Guid> AddAsync(MonitorDto dto)
    {
        try
        {
            var targetBuilder = _targetBuilders.SingleOrDefault(b => b.Type == dto.TargetType);
            if (targetBuilder == null)
            {
                throw new InvalidOperationException($"No target builder found for type: {dto.TargetType}");
            }
            var target = targetBuilder.Build(dto.TargetConfig);

            var assertions = dto.Assertions
                .Select(aDto =>
                {
                    var builder = _assertionBuilders.SingleOrDefault(b => b.Type == aDto.Type);
                    if (builder == null)
                        throw new InvalidOperationException($"Unknown assertion type: {aDto.Type}");

                    return builder.Build(aDto.ConfigJson); // 返回 AssertionRule
                })
                .ToList();

            var monitorEntity = new MonitorEntity(
                Guid.NewGuid(),
                dto.Name,
                target,                   // 从 TargetBuilder 构建
                MonitorStatus.Pending,    // 默认状态
                null,                     // 还没执行过
                true                      // 启用
            );
            foreach (var assertion in assertions)
            {
                monitorEntity.AddAssertion(assertion);
            }
            await _monitorRepository.AddAsync(monitorEntity, _unitOfWork.Transaction);
            await _unitOfWork.CommitAsync();
            return monitorEntity.Id;
        }
        catch
        {
            await _unitOfWork.RollbackAsync();
            throw;
        }
    }

    public async Task DeleteAsync(Guid id)
    {
        try
        {
            await _monitorRepository.RemoveAsync(id, _unitOfWork.Transaction);
            await _unitOfWork.CommitAsync();
        }
        catch
        {
            await _unitOfWork.RollbackAsync();
            throw;
        }
        // 删除缓存
        await _cacheService.RemoveAsync($"Monitor{id}");
    }

    public async Task<MonitorEntity?> GetByIdAsync(Guid id)
    {
        //先查缓存
        var cacheKey = $"Monitor{id}";
        var cached = await _cacheService.GetOrCreateAsync<MonitorEntity>(cacheKey, async () =>
        {
            // 缓存没有，查数据库
            var monitor = await _monitorRepository.GetByIdAsync(id);
            return monitor;
        });
        return cached;

    }

    public async Task UpdateAsync(Guid id, MonitorDto dto)
    {
        try
        {
            var existing = await _monitorRepository.GetByIdAsync(id, _unitOfWork.Transaction);
            if (existing == null)
                throw new InvalidOperationException("Monitor not found");

            var targetBuilder = _targetBuilders.FirstOrDefault(b => b.Type == dto.TargetType);
            if (targetBuilder == null)
                throw new InvalidOperationException($"No target builder for type: {dto.TargetType}");

            existing.Update(dto.Name, targetBuilder.Build(dto.TargetConfig), dto.IsEnabled);
            existing.ClearAssertions();
            foreach (var aDto in dto.Assertions)
            {
                var builder = _assertionBuilders.SingleOrDefault(b => b.Type == aDto.Type);
                if (builder == null)
                    throw new InvalidOperationException($"Unknown assertion type: {aDto.Type}");
                var assertion = builder.Build(aDto.ConfigJson);
                existing.AddAssertion(assertion);
            }

            await _monitorRepository.UpdateAsync(existing, _unitOfWork.Transaction);
            await _unitOfWork.CommitAsync();

            // 更新缓存
            await _cacheService.RemoveAsync($"Monitor{id}");
        }
        catch
        {
            await _unitOfWork.RollbackAsync();
            throw;
        }
    }

}

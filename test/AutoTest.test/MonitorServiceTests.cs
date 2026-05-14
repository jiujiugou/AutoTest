using AutoTest.Application;
using AutoTest.Application.Builder;
using AutoTest.Application.Dto;
using AutoTest.Core;
using AutoTest.Core.Assertion;
using AutoTest.Core.Execution;
using AutoTest.Core.Abstraction;
using CacheCommons;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace AutoTest.Tests.Application;

public class MonitorServiceTests
{
    private readonly Mock<IMonitorRepository> _monitorRepo = new();
    private readonly Mock<ICacheService> _cache = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IOrchestrator> _orchestrator = new();
    private readonly Mock<IExecutionRecordRepository> _execRecordRepo = new();
    private readonly Mock<ITargetMap> _targetMap = new();
    private readonly Mock<IAssertionRuleMap> _assertionMap = new();

    private MonitorService CreateSut() =>
        new(
            new[] { _targetMap.Object },
            new[] { _assertionMap.Object },
            _monitorRepo.Object,
            _cache.Object,
            _uow.Object,
            _orchestrator.Object,
            _execRecordRepo.Object,
            new NullLogger<MonitorService>());

    // ==================== AddAsync ====================

    [Fact]
    public async Task AddAsync_ShouldReturnId_WhenValidDto()
    {
        _targetMap.SetupGet(x => x.Type).Returns("HTTP");
        _targetMap.Setup(x => x.Map(It.IsAny<string>())).Returns(new TestTarget());
        _assertionMap.SetupGet(x => x.Type).Returns("HTTP");
        _assertionMap.Setup(x => x.Map(It.IsAny<Guid>(), It.IsAny<string>()))
            .Returns((Guid id, string _) => new AssertionRule(id, "HTTP", "{}"));
        _uow.Setup(x => x.ExecuteAsync(It.IsAny<Func<System.Data.IDbTransaction, Task>>()))
            .Callback<Func<System.Data.IDbTransaction, Task>>(fn => fn(null!).Wait());

        var dto = new MonitorDto
        {
            Name = "test-monitor",
            TargetType = "HTTP",
            TargetConfig = "{}",
            IsEnabled = true,
            Assertions = { new AssertionDto { Id = Guid.NewGuid(), Type = "HTTP", ConfigJson = "{}" } }
        };

        var sut = CreateSut();
        var id = await sut.AddAsync(dto);

        id.Should().NotBeEmpty();
        _monitorRepo.Verify(x => x.AddAsync(It.IsAny<MonitorEntity>(), It.IsAny<System.Data.IDbTransaction>()), Times.Once);
    }

    [Fact]
    public async Task AddAsync_ShouldThrow_WhenTargetTypeUnknown()
    {
        _targetMap.SetupGet(x => x.Type).Returns("HTTP");

        var sut = CreateSut();
        var dto = new MonitorDto { Name = "x", TargetType = "UNKNOWN", TargetConfig = "{}" };

        var act = () => sut.AddAsync(dto);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*No target builder found*");
    }

    // ==================== DeleteAsync ====================

    [Fact]
    public async Task DeleteAsync_ShouldRemoveAndClearCache()
    {
        var monitorId = Guid.NewGuid();
        var monitor = new MonitorEntity(monitorId, "m", new TestTarget(), MonitorStatus.Pending, null);
        _monitorRepo.Setup(x => x.GetByIdAsync(monitorId, It.IsAny<System.Data.IDbTransaction>()))
            .ReturnsAsync(monitor);
        _uow.Setup(x => x.ExecuteAsync(It.IsAny<Func<System.Data.IDbTransaction, Task>>()))
            .Callback<Func<System.Data.IDbTransaction, Task>>(fn => fn(null!).Wait());

        var sut = CreateSut();
        await sut.DeleteAsync(monitorId);

        _monitorRepo.Verify(x => x.RemoveAsync(monitorId, It.IsAny<System.Data.IDbTransaction>()), Times.Once);
        _cache.Verify(x => x.RemoveAsync($"Monitor{monitorId}"), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_ShouldThrow_WhenMonitorNotFound()
    {
        _monitorRepo.Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<System.Data.IDbTransaction>()))
            .ReturnsAsync((MonitorEntity?)null);
        _uow.Setup(x => x.ExecuteAsync(It.IsAny<Func<System.Data.IDbTransaction, Task>>()))
            .Callback<Func<System.Data.IDbTransaction, Task>>(fn => fn(null!).Wait());

        var sut = CreateSut();
        var act = () => sut.DeleteAsync(Guid.NewGuid());
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Monitor not found*");
    }

    // ==================== GetByIdAsync ====================

    [Fact]
    public async Task GetByIdAsync_ShouldReturnFromCache_WhenCached()
    {
        var monitorId = Guid.NewGuid();
        var monitor = new MonitorEntity(monitorId, "m", new TestTarget(), MonitorStatus.Pending, null);
        _cache.Setup(x => x.GetOrCreateAsync<MonitorEntity>(
                $"Monitor{monitorId}", It.IsAny<Func<Task<MonitorEntity?>>>()))
            .ReturnsAsync(monitor);

        var sut = CreateSut();
        var result = await sut.GetByIdAsync(monitorId);

        result.Should().Be(monitor);
        _monitorRepo.Verify(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<System.Data.IDbTransaction>()), Times.Never);
    }

    // ==================== UpdateAsync ====================

    [Fact]
    public async Task UpdateAsync_ShouldUpdateAndClearCache()
    {
        var monitorId = Guid.NewGuid();
        var monitor = new MonitorEntity(monitorId, "old", new TestTarget(), MonitorStatus.Pending, null);
        _monitorRepo.Setup(x => x.GetByIdAsync(monitorId, It.IsAny<System.Data.IDbTransaction>()))
            .ReturnsAsync(monitor);
        _targetMap.SetupGet(x => x.Type).Returns("HTTP");
        _targetMap.Setup(x => x.Map(It.IsAny<string>())).Returns(new TestTarget());
        _assertionMap.SetupGet(x => x.Type).Returns("HTTP");
        _assertionMap.Setup(x => x.Map(It.IsAny<Guid>(), It.IsAny<string>()))
            .Returns((Guid id, string _) => new AssertionRule(id, "HTTP", "{}"));
        _uow.Setup(x => x.ExecuteAsync(It.IsAny<Func<System.Data.IDbTransaction, Task>>()))
            .Callback<Func<System.Data.IDbTransaction, Task>>(fn => fn(null!).Wait());

        var dto = new MonitorDto { Name = "new", TargetType = "HTTP", TargetConfig = "{}" };

        var sut = CreateSut();
        await sut.UpdateAsync(monitorId, dto);

        _monitorRepo.Verify(x => x.UpdateAsync(monitor, It.IsAny<System.Data.IDbTransaction>()), Times.Once);
        _cache.Verify(x => x.RemoveAsync($"Monitor{monitorId}"), Times.Once);
    }

    // ==================== SetEnabledAsync ====================

    [Fact]
    public async Task SetEnabledAsync_ShouldToggleAndClearCache()
    {
        var monitorId = Guid.NewGuid();
        var monitor = new MonitorEntity(monitorId, "m", new TestTarget(), MonitorStatus.Pending, null, isEnabled: true);
        _monitorRepo.Setup(x => x.GetByIdAsync(monitorId, It.IsAny<System.Data.IDbTransaction>()))
            .ReturnsAsync(monitor);
        _uow.Setup(x => x.ExecuteAsync(It.IsAny<Func<System.Data.IDbTransaction, Task>>()))
            .Callback<Func<System.Data.IDbTransaction, Task>>(fn => fn(null!).Wait());

        var sut = CreateSut();
        await sut.SetEnabledAsync(monitorId, false);

        monitor.IsEnabled.Should().BeFalse();
        _cache.Verify(x => x.RemoveAsync($"Monitor{monitorId}"), Times.Once);
    }

    // ==================== SetScheduleAsync ====================

    [Fact]
    public async Task SetScheduleAsync_ShouldUpdateScheduleAndClearCache()
    {
        var monitorId = Guid.NewGuid();
        var monitor = new MonitorEntity(monitorId, "m", new TestTarget(), MonitorStatus.Pending, null);
        _monitorRepo.Setup(x => x.GetByIdAsync(monitorId, It.IsAny<System.Data.IDbTransaction>()))
            .ReturnsAsync(monitor);
        _uow.Setup(x => x.ExecuteAsync(It.IsAny<Func<System.Data.IDbTransaction, Task>>()))
            .Callback<Func<System.Data.IDbTransaction, Task>>(fn => fn(null!).Wait());

        var sut = CreateSut();
        await sut.SetScheduleAsync(monitorId, true, "08:00", 100);

        monitor.AutoDailyEnabled.Should().BeTrue();
        monitor.AutoDailyTime.Should().Be("08:00");
        monitor.MaxRuns.Should().Be(100);
        _cache.Verify(x => x.RemoveAsync($"Monitor{monitorId}"), Times.Once);
    }

    [Fact]
    public async Task SetScheduleAsync_ShouldDisableAuto_WhenExecutedCountReachesMaxRuns()
    {
        var monitorId = Guid.NewGuid();
        var monitor = new MonitorEntity(monitorId, "m", new TestTarget(), MonitorStatus.Pending, null,
            executedCount: 10);
        _monitorRepo.Setup(x => x.GetByIdAsync(monitorId, It.IsAny<System.Data.IDbTransaction>()))
            .ReturnsAsync(monitor);
        _uow.Setup(x => x.ExecuteAsync(It.IsAny<Func<System.Data.IDbTransaction, Task>>()))
            .Callback<Func<System.Data.IDbTransaction, Task>>(fn => fn(null!).Wait());

        var sut = CreateSut();
        await sut.SetScheduleAsync(monitorId, true, "08:00", 10);

        monitor.AutoDailyEnabled.Should().BeFalse();
    }

    // ==================== IncrementAutoExecutedCountAndDisableIfReachedAsync ====================

    [Fact]
    public async Task IncrementAutoExecutedCount_ShouldIncrementAndDisable_WhenMaxReached()
    {
        var monitorId = Guid.NewGuid();
        var monitor = new MonitorEntity(monitorId, "m", new TestTarget(), MonitorStatus.Pending, null,
            autoDailyEnabled: true, maxRuns: 5, executedCount: 4);
        _monitorRepo.Setup(x => x.GetByIdAsync(monitorId, It.IsAny<System.Data.IDbTransaction>()))
            .ReturnsAsync(monitor);
        _uow.Setup(x => x.ExecuteAsync(It.IsAny<Func<System.Data.IDbTransaction, Task>>()))
            .Callback<Func<System.Data.IDbTransaction, Task>>(fn => fn(null!).Wait());

        var sut = CreateSut();
        var disabled = await sut.IncrementAutoExecutedCountAndDisableIfReachedAsync(monitorId);

        disabled.Should().BeTrue();
        monitor.ExecutedCount.Should().Be(5);
        monitor.AutoDailyEnabled.Should().BeFalse();
        _cache.Verify(x => x.RemoveAsync($"Monitor{monitorId}"), Times.Once);
    }

    [Fact]
    public async Task IncrementAutoExecutedCount_ShouldNotDisable_WhenBelowMax()
    {
        var monitorId = Guid.NewGuid();
        var monitor = new MonitorEntity(monitorId, "m", new TestTarget(), MonitorStatus.Pending, null,
            autoDailyEnabled: true, maxRuns: 10, executedCount: 4);
        _monitorRepo.Setup(x => x.GetByIdAsync(monitorId, It.IsAny<System.Data.IDbTransaction>()))
            .ReturnsAsync(monitor);
        _uow.Setup(x => x.ExecuteAsync(It.IsAny<Func<System.Data.IDbTransaction, Task>>()))
            .Callback<Func<System.Data.IDbTransaction, Task>>(fn => fn(null!).Wait());

        var sut = CreateSut();
        var disabled = await sut.IncrementAutoExecutedCountAndDisableIfReachedAsync(monitorId);

        disabled.Should().BeFalse();
        monitor.ExecutedCount.Should().Be(5);
        monitor.AutoDailyEnabled.Should().BeTrue();
    }

    // ==================== TryStartExecutionAsync ====================

    [Fact]
    public async Task TryStartExecutionAsync_ShouldReturnStarted_WhenIdempotencyKeyIsNew()
    {
        var monitorId = Guid.NewGuid();
        var monitor = new MonitorEntity(monitorId, "m", new TestTarget(), MonitorStatus.Pending, null);
        _monitorRepo.Setup(x => x.GetByIdAsync(monitorId, It.IsAny<System.Data.IDbTransaction>()))
            .ReturnsAsync(monitor);
        _execRecordRepo.Setup(x => x.GetIdByIdempotencyKeyAsync("key1"))
            .ReturnsAsync((Guid?)null);
        _execRecordRepo.Setup(x => x.TryAddRunningAsync(
                It.IsAny<Guid>(), monitorId, It.IsAny<DateTime>(), "key1", "worker1",
                It.IsAny<DateTime>(), It.IsAny<System.Data.IDbTransaction>(), null))
            .ReturnsAsync(true);
        _uow.Setup(x => x.ExecuteAsync(It.IsAny<Func<System.Data.IDbTransaction, Task>>()))
            .Callback<Func<System.Data.IDbTransaction, Task>>(fn => fn(null!).Wait());

        var sut = CreateSut();
        var (started, executionId, _) = await sut.TryStartExecutionAsync(monitorId, "key1", "worker1");

        started.Should().BeTrue();
        executionId.Should().NotBeEmpty();
        monitor.Status.Should().Be(MonitorStatus.Running);
    }

    [Fact]
    public async Task TryStartExecutionAsync_ShouldReturnExisting_WhenIdempotencyKeyExists()
    {
        var monitorId = Guid.NewGuid();
        var existingExecutionId = Guid.NewGuid();
        _execRecordRepo.Setup(x => x.GetIdByIdempotencyKeyAsync("key1"))
            .ReturnsAsync(existingExecutionId);

        var sut = CreateSut();
        var (started, executionId, _) = await sut.TryStartExecutionAsync(monitorId, "key1", "worker1");

        started.Should().BeFalse();
        executionId.Should().Be(existingExecutionId);
        _uow.Verify(x => x.ExecuteAsync(It.IsAny<Func<System.Data.IDbTransaction, Task>>()), Times.Never);
    }

    [Fact]
    public async Task TryStartExecutionAsync_ShouldReturnStarted_WhenIdempotencyKeyIsNull()
    {
        var monitorId = Guid.NewGuid();
        var monitor = new MonitorEntity(monitorId, "m", new TestTarget(), MonitorStatus.Pending, null);
        _monitorRepo.Setup(x => x.GetByIdAsync(monitorId, It.IsAny<System.Data.IDbTransaction>()))
            .ReturnsAsync(monitor);
        _execRecordRepo.Setup(x => x.TryAddRunningAsync(
                It.IsAny<Guid>(), monitorId, It.IsAny<DateTime>(), null, "worker1",
                It.IsAny<DateTime>(), It.IsAny<System.Data.IDbTransaction>(), null))
            .ReturnsAsync(true);
        _uow.Setup(x => x.ExecuteAsync(It.IsAny<Func<System.Data.IDbTransaction, Task>>()))
            .Callback<Func<System.Data.IDbTransaction, Task>>(fn => fn(null!).Wait());

        var sut = CreateSut();
        var (started, _, _) = await sut.TryStartExecutionAsync(monitorId, null, "worker1");

        started.Should().BeTrue();
    }

    // ==================== ListAsync ====================

    [Fact]
    public async Task ListAsync_ShouldReturnFromRepository()
    {
        var monitors = new[] {
            new MonitorEntity(Guid.NewGuid(), "a", new TestTarget(), MonitorStatus.Pending, null),
            new MonitorEntity(Guid.NewGuid(), "b", new TestTarget(), MonitorStatus.Pending, null)
        };
        _monitorRepo.Setup(x => x.ListAsync(50)).ReturnsAsync(monitors);

        var sut = CreateSut();
        var result = (await sut.ListAsync()).ToList();

        result.Should().HaveCount(2);
    }

    // ==================== GetScheduleAsync ====================

    [Fact]
    public async Task GetScheduleAsync_ShouldReturnSchedule()
    {
        var monitorId = Guid.NewGuid();
        var monitor = new MonitorEntity(monitorId, "m", new TestTarget(), MonitorStatus.Pending, null,
            autoDailyEnabled: true, autoDailyTime: "09:00", maxRuns: 10, executedCount: 3);
        _cache.Setup(x => x.GetOrCreateAsync<MonitorEntity>(
                $"Monitor{monitorId}", It.IsAny<Func<Task<MonitorEntity?>>>()))
            .ReturnsAsync(monitor);

        var sut = CreateSut();
        var (enabled, time, maxRuns, count) = await sut.GetScheduleAsync(monitorId);

        enabled.Should().BeTrue();
        time.Should().Be("09:00");
        maxRuns.Should().Be(10);
        count.Should().Be(3);
    }

    // ==================== GetExecutionsAsync ====================

    [Fact]
    public async Task GetExecutionsAsync_ShouldReturnFromRepository()
    {
        var monitorId = Guid.NewGuid();
        var records = new[] { new ExecutionRecord() };
        _execRecordRepo.Setup(x => x.GetByMonitorIdAsync(monitorId, 20)).ReturnsAsync(records);

        var sut = CreateSut();
        var result = (await sut.GetExecutionsAsync(monitorId)).ToList();

        result.Should().HaveCount(1);
    }

    // ==================== Helper ====================

    private sealed class TestTarget : MonitorTarget
    {
        public override string Type => "HTTP";
        public override string ToJson() => "{}";
    }
}

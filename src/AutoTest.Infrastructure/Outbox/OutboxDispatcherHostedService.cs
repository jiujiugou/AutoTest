using AutoTest.Application;
using AutoTest.Core.AI;
using AutoTest.Core.Outbox;
using EventCommons;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace AutoTest.Infrastructure.Outbox;

/// <summary>
/// Outbox 分发后台服务
/// 
/// 核心职责：
/// 1. 周期性从 Outbox 表中拉取待发送消息
/// 2. 通过“认领 + 锁机制”避免多实例重复消费
/// 3. 使用 HTTP Webhook 将消息推送到外部系统
/// 4. 根据发送结果更新状态（成功 / 失败 / 重试）
/// 
/// 一致性模型：
/// - At-least-once（至少投递一次）
/// - 可能重复发送，需要下游幂等处理
/// </summary>
public sealed class OutboxDispatcherHostedService : BackgroundService
{

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxDispatcherHostedService> _logger;
    private readonly OutboxOptions _options;

    /// <summary>
    /// 当前实例的唯一标识（用于分布式锁）
    /// 格式：机器名 + 进程ID + GUID
    /// 
    /// 用途：
    /// - 标识“谁锁住了这条消息”
    /// - 避免多实例重复处理
    /// </summary>
    private readonly string _lockedBy;
    private DateTime _lastCleanupAt = DateTime.MinValue;

    /// <summary>
    /// 构造函数
    /// </summary>
    public OutboxDispatcherHostedService(
        IServiceScopeFactory scopeFactory,
        IOptions<OutboxOptions> options,
        ILogger<OutboxDispatcherHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options.Value;

        // 每个实例唯一，用于分布式锁标识
        _lockedBy = $"{Environment.MachineName}:{Environment.ProcessId}:{Guid.NewGuid():N}";
    }

    /// <summary>
    /// 后台循环入口
    /// 
    /// 执行逻辑：
    /// - 按固定时间间隔轮询 Outbox
    /// - 每次调用 DispatchOnceAsync 处理一批消息
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {

        // 轮询间隔（最小200ms）
        var pollInterval = TimeSpan.FromMilliseconds(Math.Max(200, _options.PollIntervalMs));

        // 锁持续时间（防止死锁，最小10秒）
        var lockDuration = TimeSpan.FromSeconds(Math.Max(10, _options.LockSeconds));

        using var timer = new PeriodicTimer(pollInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DispatchOnceAsync(lockDuration, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // 捕获所有异常，避免后台任务崩溃
                _logger.LogError(ex, "Outbox webhook dispatcher error");
            }

            if ((DateTime.UtcNow - _lastCleanupAt).TotalHours >= 1)
            {
                try
                {
                    using var cleanupScope = _scopeFactory.CreateScope();
                    var repo = cleanupScope.ServiceProvider.GetRequiredService<AutoTest.Core.Abstraction.IOutboxRepository>();
                    var cutoff = DateTime.UtcNow.AddDays(-_options.DeadLetterRetentionDays);
                    var deleted = await repo.DeleteExpiredDeadLettersAsync(cutoff, stoppingToken);
                    if (deleted > 0)
                        _logger.LogInformation("Cleaned up {Count} expired dead letter messages", deleted);
                    _lastCleanupAt = DateTime.UtcNow;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Dead letter cleanup failed");
                }
            }

            try
            {
                await timer.WaitForNextTickAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    /// <summary>
    /// 单次分发逻辑（处理一批消息）
    /// 
    /// 步骤：
    /// 1. 从 Outbox 表中“认领”一批消息（加锁）
    /// 2. 遍历发送
    /// 3. 成功 -> 标记 Sent
    /// 4. 失败 -> 标记 Failed + 设置重试时间
    /// </summary>
    private async Task DispatchOnceAsync(TimeSpan lockDuration, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();

        // 每次使用独立 scope，避免长生命周期依赖问题
        var outboxRepository = scope.ServiceProvider.GetRequiredService<AutoTest.Core.Abstraction.IOutboxRepository>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var now = DateTime.UtcNow;

        // 认领一批消息（带锁）
        var batch = await outboxRepository.LockNextBatchAsync(
            take: Math.Max(1, _options.BatchSize),
            lockDuration: lockDuration,
            lockedBy: _lockedBy,
            utcNow: now,
            cancellationToken: cancellationToken);

        if (batch.Count == 0)
            return;
        using var semaphore = new SemaphoreSlim(5);
        var task = batch.Select(async msg =>
        {
            await semaphore.WaitAsync();
            try
            {
                var payload = JsonSerializer.Deserialize<MonitorExecutionFailedPayload>(msg.PayloadJson);
                
                await mediator.Publish(new MonitorExecutionFailedEvent(payload.ExecutionId, msg.Id, msg.PayloadJson), cancellationToken);
                _logger.LogInformation("🔥 Publishing event {Id}", msg.Id);
                await outboxRepository.MarkSentAsync(
                    msg.Id, _lockedBy, DateTime.UtcNow, cancellationToken);
            }
            catch (Exception ex)
            {
                if (msg.Attempts >= _options.MaxRetryCount)
                {
                    await outboxRepository.MarkDeadLetterAsync(
                        msg.Id, _lockedBy, ex.ToString(), cancellationToken);
                    _logger.LogWarning(ex, "Message {Id} moved to DeadLetter after {Attempts} attempts",
                        msg.Id, msg.Attempts);
                }
                else
                {
                    var next = ComputeNextAttempt(msg.Attempts, DateTime.UtcNow);
                    await outboxRepository.MarkFailedAsync(
                        msg.Id, _lockedBy, ex.ToString(), next, cancellationToken);
                    _logger.LogWarning(ex, "Dispatch failed {Id}", msg.Id);
                }
            }
            finally
            {
                semaphore.Release();
            }
        });
        await Task.WhenAll(task);
    }

    

    /// <summary>
    /// 计算下一次重试时间（指数退避）
    /// 
    /// 策略：
    /// - 初始延迟：5秒
    /// - 每次失败翻倍（2^n）
    /// - 最大延迟：10分钟
    /// 
    /// 示例：
    /// 1次失败 -> 5s
    /// 2次失败 -> 10s
    /// 3次失败 -> 20s
    /// ...
    /// </summary>
    private static DateTime ComputeNextAttempt(int attempts, DateTime utcNow)
    {
        var exp = Math.Min(10, Math.Max(0, attempts - 1));
        var delaySeconds = Math.Min(600, 5 * (int)Math.Pow(2, exp));
        return utcNow.AddSeconds(delaySeconds);
    }

}
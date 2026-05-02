# 异步任务队列与调度

AutoTest 使用两种任务调度机制，分别应对不同场景。

## 6.1 监控调度（Hangfire）

定时执行监控任务由 Hangfire 负责。

### 配置

```csharp
services.AddHangfire(config =>
{
    config.UseSqlServerStorage(hangfireConnection);
});
services.AddHangfireServer(options =>
{
    options.WorkerCount = 5;
    options.SchedulePollingInterval = TimeSpan.FromSeconds(5);
});
```

### 工作流

```
Hangfire Server (每 5s 检查)
    │
    ├── WorkflowJob.RunAsync()
    │       ├── 加载 Monitor 任务
    │       ├── TryStartExecutionAsync() (幂等键防重)
    │       ├── Orchestrator.TryExecuteAsync()
    │       └── 结果 → ExecutionRecord + Outbox 事件
    │
    └── Monitor 调度配置
            ├── AutoDailyEnabled (每日自动执行)
            ├── AutoDailyTime (HH:mm)
            ├── MaxRuns (最大执行次数，达标自动禁用)
            └── ExecutedCount (已执行计数)
```

### 幂等保障

```csharp
// TryStartExecutionAsync 使用 idempotencyKey 防重
var (started, executionId, _) = await _monitorService.TryStartExecutionAsync(
    monitorId,
    idempotencyKey: $"daily-{monitorId}-{today:yyyyMMdd}",
    lockedBy: "hangfire-scheduler");
```

## 6.2 AI 任务队列（AiTask 表）

AI 分析任务使用数据库表作为队列（而非消息中间件），由 `AiWorker` (BackgroundService) 轮询消费。

### 设计理由

- 降低运维复杂度（无需 Kafka / RabbitMQ）
- 利用 DB 事务保证一致性（Outbox + AiTask 同一事务）
- 重试逻辑灵活（指数退避 + DeadLetter）

### 队列状态机

```
         EnqueueAsync()
              │
              ▼
          ┌────────┐
    ┌────▶│ Pending │◀────────────┐
    │     └───┬────┘              │
    │         │ TakeBatchAsync()  │
    │         ▼                   │
    │     ┌──────────┐            │
    │     │Processing│            │
    │     └───┬──────┘            │
    │         │                   │
    │    ┌────┴────┐              │
    │    ▼         ▼              │
    │ ┌──────┐ ┌────────┐        │
    │ │Success│ │Failed  │────────┘ (Attempts < 5)
    │ └──────┘ └───┬────┘
    │              │ (Attempts >= 5)
    │              ▼
    │         ┌───────────┐
    │         │DeadLetter │ (人工介入)
    │         └───────────┘
```

### 并发控制

```csharp
// AiWorker 并发模型
var parallelism = 4;
using var semaphore = new SemaphoreSlim(parallelism);

var running = tasks.Select(async task =>
{
    await semaphore.WaitAsync(ct);
    try { await ProcessOne(task, ct); }
    finally { semaphore.Release(); }
});
await Task.WhenAll(running);
```

- 批大小：10 条 / 轮
- 并发度：4
- 轮询间隔：1s（无任务时）

### 指数退避

```csharp
private static DateTime ComputeNext(int attempts)
{
    var exp = Math.Min(6, Math.Max(0, attempts));
    var seconds = Math.Min(300, 5 * (int)Math.Pow(2, exp));
    return DateTime.UtcNow.AddSeconds(seconds);
}
```

| Attempts | 等待时间 | 累计等待 |
|----------|---------|---------|
| 0 | 5s | 5s |
| 1 | 10s | 15s |
| 2 | 20s | 35s |
| 3 | 40s | 75s |
| 4 | 80s | 155s |
| 5+ | DeadLetter | — |

### 分布式锁设计

使用 DB 字段的乐观锁机制防止多实例冲突：

```sql
UPDATE AiTask
SET Status = 'Processing', LockedBy = @Worker, LockedAt = @Now
WHERE Id IN @Ids
-- 天然排他：另一实例不会选中已被 LockedBy 占用的记录
```

## 6.3 Outbox 调度

`OutboxDispatcherHostedService` 负责轮询 `OutboxMessage` 表并投递事件。

```
OutboxDispatcherHostedService (每 2s 轮询)
    │
    ├── LockNextBatchAsync() → 批量抢消息
    ├── MediatR.Publish()    → 触发 Handler
    │       ├── WebConsumer          → Webhook 回调
    │       └── AiAnalysisConsumer   → 入队 AiTask
    └── MarkSentAsync() / MarkFailedAsync()
```

## 6.4 三种调度对比

| 特性 | Hangfire | AiTask 表 | Outbox |
|------|---------|-----------|--------|
| 用途 | 监控执行调度 | AI 分析任务 | 事件投递 |
| 间隔 | 5s | 1s | 2s |
| 持久化 | SQL Server | AiTask 表 | OutboxMessage 表 |
| 重试 | 内置 | 指数退退 | 固定间隔 |
| 并发 | WorkerCount=5 | Semaphore=4 | 单线程 |

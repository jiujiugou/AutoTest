# 性能优化

## 12.1 日志查询优化

### ES 查询优化

```csharp
// 按 keyword 精确匹配（避免 text 分词开销）
"term": { "traceId.keyword": "{traceId}" }

// 时间范围限制（默认 ±30 秒窗口）
"range": { "@timestamp": { "gte": "now-30s", "lte": "now+30s" } }

// 限制返回条数
"size": 120
```

### 日志截断策略

| 字段 | 截断长度 | 理由 |
|------|---------|------|
| Message | 150 字符 | 控制 Token 预算 |
| StackTrace | 2048 字符 | 保留足够定位信息 |
| Exception.Message | 1000 字符 | 关键信息集中在开头 |

## 12.2 AI Worker 吞吐

### 并发控制

```csharp
// 当前配置
private const int BatchSize = 10;     // 每轮拉取
private const int Parallelism = 4;    // 最大并发
private const int PollIntervalMs = 1000;  // 空闲轮询间隔
```

### 吞吐量估算

| 场景 | 单任务耗时 | 吞吐（4并发） |
|------|-----------|-------------|
| LLM 快速响应 | 3s | ~80 任务/分钟 |
| LLM 正常响应 | 8s | ~30 任务/分钟 |
| LLM 超时（30s） | 35s | ~7 任务/分钟 |

### 调优方向

- 增大 `Parallelism` → 提高吞吐，但增加 DB 连接压力
- 减小 `PollIntervalMs` → 降低空闲延迟，但增加无谓查询
- 开启 ES 结果缓存 → 相同 traceId 的重复分析无需重复查询 ES

## 12.3 Outbox 吞吐

| 配置 | 默认值 | 说明 |
|------|--------|------|
| BatchSize | 20 | 每轮领取消息数 |
| PollingIntervalMs | 2000 | 轮询间隔 |
| LockDurationSeconds | 120 | 消息锁时长 |

### 性能瓶颈点

- **DB 轮询延迟**：`SELECT ... WHERE Status = 0` + `UPDATE ... SET Status = 1`
- **MediatR 分发**：同步调用 Handler，Handler 失败会阻塞当前 batch
- **Handler 慢操作**：`WebConsumer`（HTTP 回调）可能成为瓶颈

## 12.4 数据库优化

### 关键索引

```sql
-- AiTask 轮询优化（覆盖索引 + 筛选）
CREATE INDEX IX_AiTask_Status_NextRunAt
ON AiTask(Status, NextRunAt)
WHERE Status = 'Pending';

-- ExecutionRecord 统计优化
CREATE INDEX IX_ExecutionRecord_MonitorId_Status
ON ExecutionRecord(MonitorId, Status, StartedAt DESC);

-- Outbox 轮询优化（筛选索引）
CREATE INDEX IX_OutboxMessage_Status
ON OutboxMessage(Status)
WHERE Status = 0;
```

### 慢查询分析

执行链路中最常见的慢查询：

```sql
-- 统计查询（看板）— 按 MonitorId 分组聚合
SELECT MonitorId, COUNT(*), SUM(CASE WHEN IsExecutionSuccess=1 THEN 1 ELSE 0 END)
FROM ExecutionRecord
GROUP BY MonitorId;

-- 优化：使用物化视图或缓存（Redis/MemoryCache）兜底
```

## 12.5 缓存策略

| 缓存对象 | 缓存位置 | TTL | 说明 |
|----------|---------|-----|------|
| MonitorEntity | MemoryCache | 5min | 热点监控任务配置 |
| Dashboard 统计 | MemoryCache | 1min | 看板数据 |
| ES 查询结果 | MemoryCache | 30s | 重复的相同 traceId 查询 |

## 12.6 Token 预算控制

```csharp
// 输入约束（防止长日志打爆 Token 预算）
var maxStackTraceLength = 2048;
var maxLogMessageLength = 150;
var maxLogCount = 120;
var maxInputJsonLength = 100_000; // ~25K tokens

// 输出约束
var maxSuggestionLength = 500;
var maxRootCauseLength = 500;
var maxSummaryLength = 100;
```

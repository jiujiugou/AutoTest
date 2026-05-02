# 运维手册

## 11.1 日志

### 日志位置

| 日志类型 | 存储位置 | 说明 |
|----------|---------|------|
| 应用日志 | Serilog → Elasticsearch | 集中存储，按天索引 |
| 控制台日志 | Stdout（Docker） | Docker/K8s 环境 |
| Hangfire 日志 | Hangfire 内部表 | 任务调度日志 |
| AI Worker 日志 | Serilog | 通过 `AiWorker` Logger |

### 关键日志查询

```json
// ES 查询：查看 AI 分析失败的任务
GET ai-error-logs-*/_search
{
  "query": {
    "bool": {
      "must": [
        { "match": { "level": "WARNING" } },
        { "match_phrase": { "message": "AI task failed" } }
      ]
    }
  }
}

// 查询某个 traceId 的全链路日志
GET ai-error-logs-*/_search
{
  "query": { "term": { "traceId.keyword": "550e8400-..." } },
  "sort": [{ "@timestamp": "asc" }]
}
```

## 11.2 异常处理

### 已知异常场景

| 场景 | 表现 | 处理方式 |
|------|------|----------|
| ES 不可用 | 日志写入失败，应用降级（不阻塞业务） | 检查 ES 集群状态 |
| LLM API 超时 | AiTask 重试直至 DeadLetter | 检查 API 配额/网络 |
| DB 连接异常 | 整体服务不可用 | 检查数据库连接串/网络 |
| AiTask 堆积 | AiWorker 来不及消费 | 增加 Worker 并发度 |

### DeadLetter 处理

AiTask 重试 5 次后进入 DeadLetter 状态。恢复步骤：

```sql
-- 查看 DeadLetter 任务
SELECT Id, TaskType, Error, Attempts, CreatedAt
FROM AiTask
WHERE Status = 'DeadLetter'
ORDER BY CreatedAt DESC;

-- 人工处理后重置为重试
UPDATE AiTask SET Status = 'Pending', Attempts = 0, NextRunAt = GETUTCDATE()
WHERE Id = @Id;
```

## 11.3 监控指标

| 指标 | 来源 | 说明 |
|------|------|------|
| AiTask 队列深度 | `COUNT WHERE Status='Pending'` | 任务积压程度 |
| AiTask DeadLetter 数 | `COUNT WHERE Status='DeadLetter'` | 故障率 |
| AiWorker 处理延迟 | `CreatedAt → ProcessedAt` | 端到端延迟 |
| ES 写入速率 | ES 监控 | 日志写入压力 |
| Outbox 堆积数 | `COUNT WHERE Status=0` | 事件积压 |

## 11.4 数据清理

### ES 索引生命周期

```bash
# 通过 ILM 策略自动清理 30 天前的日志
PUT _ilm/policy/ai-error-logs-policy
{
  "policy": {
    "phases": {
      "hot":  { "min_age": "0d",  "actions": { "rollover": { "max_age": "1d" } } },
      "delete": { "min_age": "30d", "actions": { "delete": {} } }
    }
  }
}
```

### 数据库清理

```sql
-- 清理 90 天前的 AiTask 已完成记录
DELETE FROM AiTask WHERE Status = 'Success' AND CreatedAt < DATEADD(DAY, -90, GETUTCDATE());

-- 清理 180 天前的 AIAnalysis 记录
DELETE FROM AIAnalysis WHERE CreatedAt < DATEADD(DAY, -180, GETUTCDATE());
```

## 11.5 告警

| 条件 | 严重级别 | 建议动作 |
|------|---------|---------|
| AiTask DeadLetter > 10 | 高 | 检查 LLM API / Worker 日志 |
| Outbox 堆积 > 100 | 中 | 检查 OutboxDispatcher |
| ES 写入失败率 > 5% | 高 | 检查 ES 集群 |
| DB 连接数 > 80% | 高 | 检查连接池/慢查询 |

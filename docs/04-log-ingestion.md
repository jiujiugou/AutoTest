# 日志采集与 Elasticsearch 设计

## 4.1 日志架构

```
┌──────────────┐    ┌───────────────┐    ┌─────────────────┐
│  服务进程     │───▶│  Serilog      │───▶│  Elasticsearch  │
│ (AutoTest)   │    │  + LogContext │    │  (集中存储)      │
└──────────────┘    └───────────────┘    └────────┬────────┘
                                                  │
                    ┌──────────────────────────────┤
                    │                              │
              ┌─────▼─────┐              ┌────────▼────────┐
              │ SignalR   │              │   AiWorker      │
              │ LogHub    │              │   (LLM 分析)    │
              │ (实时推送) │              │   (日志查询)    │
              └───────────┘              └─────────────────┘
```

## 4.2 Elasticsearch 索引设计

### 索引命名

```
ai-error-logs-{yyyy-MM-dd}
```

按天分索引，便于 ILM（索引生命周期管理）自动清理过期数据。

### Mapping 结构

```json
{
  "mappings": {
    "properties": {
      "@timestamp":    { "type": "date" },
      "level":         { "type": "keyword" },
      "message":       { "type": "text" },
      "messageTemplate": { "type": "text" },
      "traceId":       { "type": "keyword" },
      "service":       { "type": "keyword" },
      "machineName":   { "type": "keyword" },
      "exception":     {
        "properties": {
          "type":    { "type": "keyword" },
          "message": { "type": "text" },
          "stackTrace": { "type": "text" }
        }
      },
      "properties":    { "type": "object", "enabled": false }
    }
  }
}
```

### 关键字段说明

| 字段 | 来源 | 用途 |
|------|------|------|
| `traceId` | `Serilog LogContext` 注入 | AI 分析时按 traceId 聚合日志 |
| `service` | `LogContext` 或配置 | 分布式环境下区分日志来源 |
| `@timestamp` | Serilog 自动记录 | 时间范围过滤 + 排序 |
| `level` | Serilog 级别 | 过滤 ERROR / WARN 级别日志 |
| `exception` | Serilog 捕获异常 | AI 分析异常堆栈 |

## 4.3 traceId 传播机制

### 注入点

```csharp
// 执行开始时注入 traceId
using (LogContext.PushProperty("traceId", executionId.ToString()))
{
    // 整个执行链路中的所有日志自动带 traceId
    _logger.LogInformation("开始执行监控任务 {MonitorId}", monitorId);
    _logger.LogError(ex, "执行失败");
}
```

### 三层 traceId 模型

```
执行级别：ExecutionId (Guid)
    │ 由 ExecutionRecord 生成，作为 traceId 注入日志
    ▼
服务级别：服务名 (Service A / B / C)
    │ 由 LogContext 注入，用于区分日志来源
    ▼
分析级别：AiTask.BizId → AIAnalysis.OutboxMessageId
    用于关联分析结果到原始事件
```

## 4.4 日志查询接口

`ILogService` 核心方法：

```csharp
// AI 分析用：按 traceId 查询跨服务日志时间线
Task<List<TraceLogEntry>> GetAiErrorContextAsync(
    string traceId,
    DateTime? errorTime = null,
    int windowSeconds = 30,
    int take = 120
);

// 前端用：分页查询日志
Task<PagedResult<LogEntry>> QueryAsync(LogQueryDto query);
```

查询优化：
- 使用 `traceId.keyword` 精确匹配（避免 text 分词）
- 时间窗口默认 ±30 秒
- 日志消息截断至 150 字符（控制 Token 预算）

## 4.5 SignalR 实时日志流

```
LogController / SignalR
    │
    ├── POST /api/logs/batch  → 批量写入 ES
    ├── SignalR LogHub        → 实时推送给前端
    └── SignalRLogSink        → Serilog Sink 转发到 SignalR
```

前端订阅 `/hubs/logs` 即可实时接收日志流。

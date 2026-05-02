# 全链路数据流

AutoTest 的核心数据流分为两条链路：**执行链路**（定时任务→执行→断言→结果）和 **分析链路**（失败事件→AI→根因分析→修复建议）。两条链路通过 Outbox 事件桥接。

## 3.1 执行链路

```
Hangfire Trigger
      │
      ▼
Orchestrator.TryExecuteAsync()
      │
      ├── 1. TryStartExecutionAsync() → INSERT ExecutionRecord(Status=Running)
      │
      ├── 2. IExecutionEngine.ExecuteAsync()
      │      └── HTTP / TCP / DB / Python 引擎执行目标
      │
      ├── 3. AssertionEngine.EvaluateAsync()
      │      └── 遍历所有断言规则，生成 AssertionResult[]
      │
      ├── 4. UpdateCompletionAsync()
      │      └── UPDATE ExecutionRecord(Status, IsExecutionSuccess, ErrorMessage)
      │
      ├── 5. 写入 OutboxMessage
      │      └── MonitorExecutionFailedEvent (仅失败时)
      │
      └── 6. 实时推送 SignalR (MonitorHub)
```

### 数据流中的关键字段传递

```
MonitorEntity (TargetConfig + Assertions)
    │
    ▼
ExecutionRecord (Id = executionId, MonitorId, Status, ErrorMessage)
    │
    ├── traceId = executionId.ToString()  ← 通过 Serilog LogContext 注入
    │       │
    │       ▼ 写入 Elasticsearch
    │   ElasticLogDocument (traceId, service, level, message, timestamp)
    │
    └── 失败时 →
        OutboxMessage (Payload = MonitorExecutionFailedPayload)
```

## 3.2 分析链路

```
MonitorExecutionFailedEvent (MediatR)
      │
      ▼
AiAnalysisConsumer.Handle()
      │
      ├── 1. 反序列化 Payload → AiAnalysisInputDto
      │
      ├── 2. EnqueueAsync() → INSERT AiTask(Status=Pending)
      │
      ▼
AiWorker.ExecuteAsync()  (轮询队列，每 1s)
      │
      ├── 3. TakeBatchAsync() → SELECT Pending AiTask
      │
      ├── 4. ProcessOne():
      │       ├── Deserialize InputJson → AiAnalysisInputDto
      │       ├── logService.GetAiErrorContextAsync(traceId)
      │       │       └── ES 查询：按 traceId 聚合跨服务日志
      │       ├── AiAnalysisPromptBuilder.BuildPrompt()
      │       │       └── SystemPrompt + UserPrompt(错误快照 + 日志时间线)
      │       ├── SkAiClient.AnalyzeAsync() → LLM 推理
      │       └── 解析 JSON → AIAnalysis(结果落库)
      │
      └── 5. MarkCompletedAsync() / MarkFailedAsync()
```

## 3.3 日志时间线构建流程

AiWorker 在调用 LLM 之前，会构建一个"日志时间线"作为推理上下文：

```
traceId = executionId.ToString()
    │
    ▼
Elasticsearch Query:
  GET ai-error-logs-*/_search
  {
    "query": {
      "bool": {
        "filter": [
          { "term": { "traceId.keyword": "{traceId}" } },
          { "range": { "@timestamp": { "gte": "now-30s", "lte": "now+30s" } } }
        ]
      }
    },
    "sort": [{ "@timestamp": "asc" }],
    "size": 120
  }
    │
    ▼
TraceLogEntry[] (按时间排序，混合多个服务的日志)
    │
    ▼
Markdown Table 格式（传递给 LLM）:
  | # | Time | Service | Level | Message |
  |---|------|---------|-------|---------|
  | 1 | 10:00:01 | auth | ERROR | 连接超时 |
  | 2 | 10:00:02 | api | WARN | 上游超时 |
```

## 3.4 自动恢复链路

```
AIAnalysis (Suggestion + Confidence)
    │
    ▼
RecoverySuggestionParser
    │   └── 关键词匹配 → Retry / ConfigFix / InfraFix / Notify
    ▼
AutoRecoveryService.ExecuteRecoveryAsync()
    │
    ├── Retry     → TryStartExecutionAsync(幂等键) → 重新执行监控
    ├── ConfigFix → 记录 Pending，等待人工确认
    ├── InfraFix  → 记录 Pending，等待人工介入
    └── Notify    → 记录 Success（后续对接钉钉/飞书）
    │
    ▼
AutoRecoveryRecord (落库追踪)
```

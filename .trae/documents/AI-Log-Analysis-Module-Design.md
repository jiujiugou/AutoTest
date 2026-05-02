# AI 日志分析模块 — 技术规范文档

> 版本: 1.1 | 日期: 2026-04-27 | 状态: Draft

---

## 1. 系统目标

构建一个**生产级 AI 日志分析模块**，接入分布式系统的错误日志流，通过大语言模型（LLM）自动完成：错误分类 → 根因分析 → 影响评估 → 修复建议 → 结构化输出，降低人工排障成本。

### 核心能力

| 能力 | 说明 | 优先级 |
|------|------|--------|
| 自动错误分类 | 识别代码缺陷 / 性能 / 依赖 / 数据库 / 安全等类型 | P0 |
| 根因分析 | 基于日志时间线 + 异常堆栈 + 断言信息，定位根本原因 | P0 |
| 影响范围判断 | 单请求 / 模块级 / 系统级 | P1 |
| 修复建议 | 输出可执行建议（代码 / 配置 / 基础设施） | P0 |
| 置信度评分 | 每个结论附带 0~1 置信度 | P1 |
| 结构化输出 | JSON 格式，直接落库与自动化消费 | P0 |

---

## 2. 系统架构

### 2.1 整体流程

```
┌──────────────┐     ┌──────────────────┐     ┌──────────────────────┐
│ 服务执行失败   │────▶│ Outbox + MediatR  │────▶│ AiAnalysisConsumer  │
│ (Orchestrator)│     │ (事件总线)        │     │ (构造失败事件DTO)   │
└──────────────┘     └──────────────────┘     └──────────┬───────────┘
                                                          │
                                                          ▼
┌──────────────┐     ┌──────────────────┐     ┌──────────────────────┐
│ AiTask DB    │◀────│ IAiTaskService   │◀────│ EnqueueAsync         │
│ (任务队列)    │     │ (任务持久化)      │     │ (写入AiTask.InputJson)│
└──────┬───────┘     └──────────────────┘     └──────────────────────┘
       │
       ▼ 轮询 (每1秒)
┌──────────────┐     ┌──────────────────┐     ┌──────────────────────┐
│ AiWorker     │────▶│ 反序列化 InputJson│────▶│ 构建 AI Context      │
│ (Background  │     │ (AiAnalysisInput) │     │ (错误快照 + 日志)    │
│  Service)    │     └──────────────────┘     └──────────┬───────────┘
└──────────────┘                                          │
                                                          ▼
┌──────────────┐     ┌──────────────────┐     ┌──────────────────────┐
│ Elasticsearch│◀────│ LogService       │     │ 构建 Prompt          │
│ (日志存储)    │     │ (GetAiErrorCtx)  │     │ (System + User)      │
└──────────────┘     └──────────────────┘     └──────────┬───────────┘
                                                          │
                                                          ▼
┌──────────────┐     ┌──────────────────┐     ┌──────────────────────┐
│ AIAnalysis   │◀────│ SkAiClient       │◀────│ Semantic Kernel      │
│ (结果落库)    │     │ (LLM 调用)       │     │ + LLM 推理            │
└──────────────┘     └──────────────────┘     └──────────────────────┘
                                                          │
                                                          ▼
┌──────────────────────────────────────────────────────────────────┐
│                         LLM 输出 JSON                            │
│  { Type, Severity, Category, RootCause, Suggestion, Summary,    │
│    Impact, Confidence }                                         │
└──────────────────────────────────────────────────────────────────┘
```

### 2.2 分布式架构全景

```
                         ┌──────────────────────────────────────┐
                         │          Serilog + Elasticsearch     │
                         │  (集中式日志聚合层)                   │
                         │  Enrich.FromLogContext()              │
                         │  → 自动注入 traceId / ExecutionId    │
                         └───────────┬──────────────────────────┘
                                     │ 写入 ES
       ┌──────────┐    ┌──────────┐ │ ┌──────────┐    ┌──────────┐
       │ Service A │    │ Service B │ │ │ Service C │    │ Service D │
       │ (AutoTest)│    │ (API网关) │ │ │ (Auth)   │    │ (DB)     │
       └─────┬─────┘    └─────┬─────┘ │ └─────┬────┘    └────┬─────┘
             │                │        │       │              │
             └────────────────┴────────┴───────┴──────────────┘
                                      │
                                      ▼
                          ┌──────────────────────┐
                          │   AiWorker (单进程)   │
                          │   并发度: 4           │
                          │   批处理: 10          │
                          └──────────┬───────────┘
                                     │
                          ┌──────────▼───────────┐
                          │   LLM (豆包/SK)       │
                          └──────────────────────┘
```

**关键要点：**
- **所有服务共用一个 Elasticsearch 集群**，日志按 `traceId.keyword` 索引
- **AiWorker 为单进程/单实例**，通过 Db 锁（LockedBy/LockedAt）防多实例冲突
- **LLM 只看到一个按 traceId 聚合后的完整视图**，不需要理解分布式拓扑

### 2.3 组件职责

| 组件 | 项目 | 职责 |
|------|------|------|
| `AiAnalysisConsumer` | Infrastructure | 消费 `MonitorExecutionFailedEvent`，构建 `AiAnalysisInputDto`，入队 `AiTask` |
| `AiTaskService` | Infrastructure | 任务队列 CRUD，批量抢任务，失败重试（指数退避） |
| `AiWorker` | Infrastructure | 轮询任务，构建上下文，调用 LLM，写回结果 |
| `LogService` | Infrastructure | 基于 traceId + 时间窗口从 ES 拉取日志 |
| `TraceContextBuilder` | AI | 将日志格式化为 LLM 优化的 Markdown |
| `SkAiClient` | AI | Semantic Kernel 封装，LLM 调用入口 |
| `KernelFactory` | AI | 注册插件、配置模型、构建 Kernel 实例 |

---

## 3. 分布式 TraceId 传播机制

### 3.1 traceId 生命周期（分布式视角）

```
                                                          traceId 传播路径
                                                          ================
 外部请求
    │
    ▼
┌─────────────┐     LogContext: traceId = "req_abc"
│ API Gateway  │────▶ Serilog PushProperty("traceId", "req_abc")
│ (Ingress)    │     写入 ES: { traceId: "req_abc", level: "INFO", ... }
└──────┬──────┘
       │ HTTP 调用 (Header: X-Trace-Id: req_abc)
       ▼
┌─────────────┐     LogContext: traceId = "req_abc"
│ AutoTest    │────▶ Serilog PushProperty("traceId", "req_abc")
│ (Orchestr.) │     写入 ES: { traceId: "req_abc", level: "INFO", ... }
└──────┬──────┘
       │ 执行监控任务
       ▼
┌─────────────┐     ExecutionId = Guid.NewGuid()  ← traceId 子ID
│ Execution   │────▶ OutboxMessage 事件
│ Engine      │     Payload.ExecutionId = executionId
└──────┬──────┘
       │ 失败
       ▼
┌─────────────┐     BuildFailurePayload()
│ Orchestrator│      Payload = { ExecutionId, Exception, ErrorMessage, Assertions }
│             │      OutboxMessage.Payload = JsonSerializer.Serialize(payload)
└─────────────┘
```

### 3.2 traceId 关系模型

```
服务级 traceId (HTTP Request Scope)
  │
  ├─ 入口: API Gateway / 外部调用方
  ├─ 存储: Serilog LogContext ("traceId")
  ├─ 传播: HTTP Header: X-Trace-Id
  └─ 生命周期: 一次完整的外部请求
        │
        ├─ 执行级 traceId (ExecutionId)
        │    ├─ 入口: Orchestrator.TryStartExecutionAsync()
        │    ├─ 存储: OutboxMessage / AiAnalysisInputDto.TraceId
        │    ├─ 用途: 日志时间线查询的关联键
        │    └─ 生命周期: 一次监控任务的单次执行
        │
        └─ 分析级 traceId (AiTask.Id)
             ├─ 入口: AiAnalysisConsumer.Handle()
             ├─ 存储: AiTask.Id / BizId
             ├─ 用途: 任务队列的去重和幂等
             └─ 生命周期: AI 分析任务的生命周期
```

**关键设计决策：**
- LLM 收到的 `traceId` 是 `ExecutionId`（Guid），而不是服务级 traceId
- 原因：`ExecutionId` 在系统中唯一确定一次执行，且已作为日志的 tag 写入 ES
- 服务级 traceId 可能跨多个执行，不适合作为分析粒度

### 3.3 Serilog 日志链路注入

```csharp
// LoggingServiceExtensions.cs 中的配置
Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()          // ← 关键：允许动态注入上下文属性
    .WriteTo.Elasticsearch(...)       // 写入 ES，自动包含所有注入的属性
    .CreateLogger();

// 在 Orchestrator 执行时注入 traceId
using (LogContext.PushProperty("traceId", executionId.ToString()))
{
    _logger.LogInformation("开始执行监控任务 {MonitorId}", monitorId);
    // 此后所有日志都自动携带 traceId = executionId
}
```

ES 中的日志文档结构：
```json
{
  "@timestamp": "2026-04-27T10:30:00Z",
  "level": "Error",
  "message": "HTTP request failed with status 500",
  "traceId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",  // ← Serilog 自动注入
  "exception": "...",
  "service": "AutoTest.Execution"
}
```

---

## 4. 数据输入结构

### 4.1 错误快照 (AiAnalysisInputDto)

由 `AiAnalysisConsumer` 从失败事件 payload 构建，序列化为 JSON 后存入 `AiTask.InputJson`。

```json
{
  "exceptionType": "NullReferenceException",
  "errorMessage": "Object reference not set to an instance of an object",
  "stackTrace": "   at AutoTest.Execution.Http.HttpExecutor.ExecuteAsync() ...",
  "traceId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "failedAssertions": [
    {
      "target": "response.status_code",
      "message": "Expected 200 but got 500"
    }
  ]
}
```

**字段说明：**

| 字段 | 类型 | 来源 | 说明 |
|------|------|------|------|
| `exceptionType` | string? | Payload.Exception.Type | 异常类型全名 |
| `errorMessage` | string? | Payload.ErrorMessage | 错误描述 |
| `stackTrace` | string? | Payload.Exception.StackTrace | 堆栈（>2048 字符时裁剪） |
| `traceId` | string? | Payload.ExecutionId | **全系统唯一关联键** |
| `failedAssertions` | array? | Payload.Assertions | 失败断言列表 |

### 4.2 日志上下文 (TraceLogEntry)

由 `LogService.GetAiErrorContextAsync` 从 Elasticsearch 查询返回。

```csharp
public class TraceLogEntry
{
    public DateTime Timestamp { get; set; }       // @timestamp
    public string Level { get; set; }             // Information / Warning / Error / Fatal
    public string Message { get; set; }           // 日志消息
    public string? Exception { get; set; }        // 完整异常堆栈（可选）
}
```

**查询条件（分布式日志聚合）：**
- `traceId.keyword` = traceId（精确匹配）— **跨所有服务的所有日志**
- `level` = ERROR OR Fatal（过滤关注级别）
- `@timestamp` 在 `[errorTime - windowSeconds, errorTime + windowSeconds]` 范围内
- 按时间升序排序
- **结果中可能包含多个服务的日志**，按时间混合排列

**分布式场景示例：**
```
时间线 | 服务         | 级别   | 消息
10:29:45 | API Gateway  | INFO   | Received request
10:29:46 | AutoTest     | INFO   | Starting execution
10:29:47 | Execution    | WARN   | Retry attempt 1/3
10:29:48 | Auth         | INFO   | User authenticated
10:29:49 | Execution    | ERROR  | HTTP 500 from downstream
10:29:50 | Execution    | FATAL  | Execution failed: NullReferenceException
```

LLM 看到的是**按时间排列的多服务日志流**，可以从中判断错误的传播路径。

### 4.3 AI 分析结果 (AIAnalysis)

LLM 输出的结构化结果，由 `SkAiClient.AnalyzeAsync` 返回 JSON，经 `AiWorker` 存入 `AiTask.OutputJson`，并写入 `AIAnalysis` 表。

```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "outboxMessageId": "660e8400-e29b-41d4-a716-446655440001",
  "type": "TestFailure",
  "severity": "high",
  "category": "NULL_REFERENCE",
  "rootCause": "HttpExecutor.ExecuteAsync() 中未对 response body 做 null 检查...",
  "suggestion": "在第 42 行添加空值保护：if (response?.Body == null)...",
  "summary": "HttpExecutor 在执行 POST /api/order 时收到 null response",
  "confidence": 0.92,
  "impact": "single_request",
  "inputJson": "{...}",
  "outputJson": "{...}",
  "model": "doubao-pro",
  "promptVersion": "v1.0",
  "createdAt": "2026-04-27T10:30:00Z",
  "processedAt": "2026-04-27T10:30:05Z"
}
```

---

## 5. 分布式上下文构建流程

### 5.1 AiWorker.ProcessOne 完整流程

```
AiWorker.ProcessOne(task)
│
├─ 1. 反序列化 InputJson → AiAnalysisInputDto
│    (从 AiTask.InputJson 中解析)
│
├─ 2. 从 AiAnalysisInputDto 提取 traceId
│    (空则跳过日志查询)
│
├─ 3. 调用 TraceContextBuilder.BuildTraceContextAsync(traceId, errorTime, 30, 120)
│    ├─ 调用 ILogService.GetAiErrorContextAsync(traceId, errorTime, windowSeconds, 120)
│    │    └─ Elasticsearch 查询 traceId.keyword = traceId
│    │       └─ 返回跨多服务的、按时间排序的日志列表
│    └─ 格式化 Markdown 时间线表格
│
├─ 4. 如果 inputDto 有 FailedAssertions
│    ├─ 附加失败断言信息
│
├─ 5. 构建完整 Prompt
│    ├─ System Prompt (角色定位 + 任务说明 + 输出格式约束)
│    └─ User Prompt (错误快照 + 日志上下文 Markdown)
│
├─ 6. SkAiClient.AnalyzeAsync(prompt) → LLM 返回 JSON
│
├─ 7. 解析 JSON 为 AiAnalysisOutputDto (验证结构完整性)
│
├─ 8. AiTaskService.MarkCompletedAsync(id, outputJson)
│    └─ 更新 AiTask 状态 + 写入 AIAnalysis 表
│
└─ 9. 异常时 AiTaskService.MarkFailedAsync(id, error, nextRunAt)
     └─ 指数退避重试（最大 5 次后 DeadLetter）
```

### 5.2 上下文融合策略

采用**分层上下文架构**，而非硬合并：

```
┌─────────────────────────────────────────────────┐
│ Final Prompt (User部分)                          │
│                                                  │
│  [Section 1: 错误快照 — 必须]                     │
│  - ExceptionType / ErrorMessage / StackTrace     │
│  - FailedAssertions                              │
│  - TraceId                                       │
│                                                  │
│  [Section 2: 日志时间线 — 可选增强]               │
│  - Markdown 表格格式                             │
│  - 包含多服务日志，按时间混合排列                 │
│  - 每行标记服务来源（如 Elasticsearch 的 service  │
│    字段，需 LogService 查询时额外返回）           │
│  - 仅当 ES 查到日志时附加                         │
│                                                  │
│  [Section 3: 分析指令]                           │
│  - 请按以下 JSON Schema 输出结构化结果...         │
└─────────────────────────────────────────────────┘
```

### 5.3 分布式日志聚合的关键增强

当前 `ElasticLogDocument` 缺少 `service` 字段，建议增强以支持 LLM 跨服务分析：

```csharp
public class ElasticLogDocument
{
    public DateTime Timestamp { get; set; }
    public string? Level { get; set; }
    public string? Message { get; set; }
    public string? Exception { get; set; }
    public string? Service { get; set; }        // 新增：来源服务名
}
```

对应的日志时间线 Markdown 增加服务列：

```
| # | Time     | Service         | Level | Message                          |
|---|----------|-----------------|-------|----------------------------------|
| 1 | 10:29:45 | API Gateway     | INFO  | Received request for /api/order  |
| 2 | 10:29:46 | AutoTest        | INFO  | Starting execution: exec-123     |
| 3 | 10:29:47 | Execution       | WARN  | HTTP 500 from /api/downstream    |
| 4 | 10:29:48 | Execution       | ERROR | NullReferenceException ...       |
```

### 5.4 Token 预算管理

| 组件 | 预估 Token | 备注 |
|------|-----------|------|
| System Prompt | ~800 | 固定开销 |
| 错误快照 | ~500 | ~2KB 堆栈 |
| 日志时间线（120条） | ~3000 | 按 150 字符/条截断 |
| 失败断言 | ~200 | 通常 1~5 条 |
| 模型输出 | ~500 | 结构化 JSON |

**策略：**
- 日志消息截断至 150 字符/条
- 堆栈截断至 2048 字符
- 异常信息截断至 1000 字符
- 超出时使用 `...` 标记截断

---

## 6. AI 分析流程

### 6.1 分析步骤拆解（分布式增强版）

LLM 按以下逻辑链逐步推理：

**Step 1: 信息提取**
```
从错误快照中提取：
- 异常类型 + 消息 → 判断错误性质
- 堆栈前 10 行 → 定位代码入口 + 判断代码归属服务
- 失败断言 → 判断预期 vs 实际
```

**Step 2: 日志分析（跨服务）**
```
扫描日志时间线：
- 标记不同服务的日志 → 判断错误发生在哪个服务
- 错误发生前：服务A是否有 WARN / 超时 / 重试 → 查找诱因
- 错误发生后：服务B/C 是否有级联错误 → 判断扩散路径
- HTTP 调用日志 → 判断上下游调用关系
- 异常堆栈 → 判断是当前服务问题还是依赖问题
```

**Step 3: 分布式根因推断**
```
基于跨服务日志，判断：
- 代码缺陷：空指针 / 逻辑错误 / 边界条件（定位到具体服务）
- 性能问题：慢查询 / 超时 / OOM（判断瓶颈位置）
- 依赖问题：下游服务异常 / 网络故障 / 配置错误
- 数据库问题：连接池耗尽 / 死锁 / 数据不一致
- 安全问题：鉴权失败 / SQL 注入

分布式特有的根因模式：
- "服务A超时 → 服务B重试 → 服务B资源耗尽 → 服务B崩溃"
  根因：服务A的慢响应
- "网关调用服务A → 服务A调用服务B → 服务B返回500"
  根因：服务B的接口缺陷
```

**Step 4: 影响范围评估**
```
- 单请求级：特定参数导致的偶发故障（一个 traceId 下的单条日志）
- 模块级：某个服务/接口持续异常（多个 traceId 出现相同模式）
- 系统级：多处服务同时出现关联错误（跨 traceId 的日志聚合）
```

**Step 5: 输出结构化 JSON**
```
按预定义 Schema 输出，字段不得缺失
```

### 6.2 AI 驱动的分布式故障模式识别

LLM 需要识别的典型分布式故障模式：

| 模式 | 日志特征 | 典型根因 |
|------|----------|----------|
| **级联超时** | 服务A WARN(超时) → 服务B WARN(超时) → 服务C ERROR(超时) | 最下游服务慢 |
| **级联崩溃** | 服务A ERROR → 服务B ERROR → 服务C ERROR | 上游服务异常传播 |
| **资源耗尽** | ERROR(连接池耗尽) → ERROR(拒绝连接) → ERROR(OOM) | 慢请求堆积 |
| **死锁/竞争** | 多条 ERROR(超时) 同时出现，时间完全重叠 | 锁竞争 / DB死锁 |
| **网络抖动** | 服务A ERROR(连接重置) → 重试成功 → 正常 | 瞬时网络故障 |
| **配置漂移** | 服务A 和 服务B 出现相同类型的错误 | 配置中心错误 |

### 6.3 输出 JSON 规范

```json
{
  "type": "TestFailure | ApiError | PerformanceIssue | SecurityIssue",
  "severity": "low | medium | high | critical",
  "category": "NULL_REFERENCE | TIMEOUT | DB_CONNECTION | DEPENDENCY_FAILURE | CASCADE_FAILURE | ...",
  "summary": "一句自然语言摘要（<100字）",
  "rootCause": "根因详细分析（<500字），需包含故障服务名",
  "suggestion": "修复建议（<500字），需要可执行",
  "impact": "single_request | module_level | system_level",
  "faultService": "触发故障的服务名（如 AutoTest.Execution）",
  "confidence": 0.0 ~ 1.0,
  "errorChain": [
    {"service": "Execution", "type": "诱因", "detail": "收到下游500响应"},
    {"service": "AutoTest", "type": "故障", "detail": "空指针处理失败响应"},
    {"service": null,       "type": "后果", "detail": "监控任务标记为失败"}
  ]
}
```

**Schema 验证规则：**

| 字段 | 必填 | 允许值/约束 |
|------|------|------------|
| `type` | ✅ | 枚举值之一 |
| `severity` | ✅ | 枚举值之一 |
| `category` | ✅ | 非空字符串 |
| `summary` | ✅ | 长度 10~100 |
| `rootCause` | ✅ | 长度 50~500，包含故障服务 |
| `suggestion` | ✅ | 长度 50~500 |
| `impact` | ✅ | 枚举值之一 |
| `faultService` | ❌ | 可选，标识触发故障的服务 |
| `confidence` | ✅ | 0.0 ~ 1.0，两位小数 |
| `errorChain` | ❌ | 可选，数组，最长 5 条 |

---

## 7. Prompt 设计方案

### 7.1 System Prompt（分布式增强版）

```
你是一个严谨的分布式系统故障分析专家。你的任务是基于提供的错误快照和跨服务日志时间线，
分析系统故障的原因，并输出结构化的 JSON 分析结果。

## 分析规则
1. 先阅读错误快照，理解错误的类型和代码位置
2. 再阅读日志时间线，注意日志来自多个不同的服务
3. 按时间顺序追踪错误的传播路径
4. 区分直接原因（哪个服务抛出的）和根本原因（为什么这个服务会失败）
5. 评估影响范围：单请求 / 模块级 / 系统级
6. 给出可执行的修复建议

## 分布式故障模式识别指南
- 级联超时：多个服务依次超时 → 根因通常是最下游服务
- 级联崩溃：一个服务异常扩散到其他服务 → 根因通常是上游服务
- 资源耗尽：连接池/OOM错误 → 根因通常是请求堆积
- 网络问题：偶发超时 + 重试成功 → 瞬时网络故障

## 错误分类指南
- 代码缺陷：NullReference、IndexOutOfRange、ArgumentNull 等
- 性能问题：Timeout、慢查询、OOM
- 依赖问题：HTTP调用失败、数据库连接失败、下游服务错误
- 数据库问题：连接池耗尽、主键冲突、死锁
- 安全问题：授权失败、权限不足

## 输出约束
- 必须输出严格有效的 JSON，不得包含多余文字
- 不得在 JSON 前后添加 markdown 代码块标记
- confidence < 0.6 时，rootCause 和 suggestion 应注明"需人工确认"
- 如果信息不足以判断，将 type 设为 "Unknown"，confidence < 0.3
```

### 7.2 User Prompt 模板

```
## 错误快照
- **异常类型**: {exceptionType}
- **错误消息**: {errorMessage}
- **Trace ID**: {traceId}
- **堆栈信息**:
```
{stackTrace}
```
- **失败断言**: {failedAssertions}

## 日志时间线（±{windowSeconds}秒，共{count}条）
{markdownTimeline}

---

请基于以上信息进行分析，按以下 JSON Schema 输出：
{outputSchema}
```

### 7.3 Prompt 版本管理

- `PromptVersion` 字段记录当前 prompt 版本（如 `v1.0`）
- 版本变更时，旧的历史数据可追溯
- A/B 测试不同 prompt 版本的效果

---

## 8. 多实例 Worker 的分布式锁设计

### 8.1 抢锁机制

AiWorker 支持多实例部署，通过数据库行锁避免任务重复处理：

```
实例A 和 实例B 同时轮询
        │
        ▼
SELECT Id FROM AiTask
WHERE Status = 'Pending' AND NextRunAt <= @Now
        │
        ▼ 两实例都查到相同的 10 个 Id
        │
UPDATE AiTask
SET Status = 'Processing', LockedBy = @Worker, LockedAt = @Now
WHERE Id IN @Ids
        │
        ▼ SQL Server 行锁保证同一行只有一个 UPDATE 成功
        │
SELECT ... FROM AiTask WHERE Id IN @Ids
        │
        ▼ 每个实例只处理自己成功锁定的任务
```

### 8.2 死锁恢复

- 如果 Worker 实例崩溃，锁定的任务永远停留在 `Processing` 状态
- `ExecutionWatchdogHostedService` 定期扫描超时的 `Processing` 任务
- 超时阈值：5 分钟
- 处理方式：重置为 `Pending`，Attempts 不变

---

## 9. 工程设计原则与约束

### 9.1 核心原则

| 原则 | 说明 |
|------|------|
| **traceId 为唯一关联键** | 所有日志查询、事件关联、结果回溯都以 traceId 为核心 |
| **Worker 负责上下文构建** | LLM 不直接访问数据库或 ES，所有数据由 Worker 获取并格式化 |
| **结构化输出** | LLM 不返回自然语言，只返回 JSON，便于程序消费 |
| **幂等任务处理** | AiTask 通过 Id + Status 保证至少一次处理语义 |
| **隔离性** | 每个 traceId 的分析结果独立，互不影响 |
| **跨服务透明** | LLM 不需要知道分布式拓扑，只需看聚合后的时间线 |

### 9.2 数据库依赖

| 依赖 | 用途 | 关键查询 |
|------|------|---------|
| Elasticsearch | 分布式日志存储 | `traceId.keyword` + `@timestamp` 范围查询（跨服务） |
| SQL Server / SQLite | AiTask 任务队列 | Status + NextRunAt 轮询 |

### 9.3 可靠性设计

```
任务重试机制：
  Attempts 0 → 5s 后重试
  Attempts 1 → 10s 后重试
  Attempts 2 → 20s 后重试
  Attempts 3 → 40s 后重试
  Attempts 4 → 80s 后重试
  Attempts ≥5 → DeadLetter（人工介入）

 公式：interval = min(300, 5 * 2^attempts)
```

### 9.4 限流与并发

| 参数 | 值 | 说明 |
|------|-----|------|
| 批处理大小 | 10 | AiWorker.TakeBatchAsync 每次拉取 |
| 并发度 | 4 | SemaphoreSlim 控制同时处理数 |
| 轮询间隔 | 1s | 无任务时的休眠时间 |
| 单次日志上限 | 120 条 | GetAiErrorContextAsync take 参数 |
| LLM 超时 | 30s | Http -> LLM 请求超时 |

---

## 10. 可扩展方向

### 10.1 多模型支持

```csharp
public interface IAiClient
{
    Task<string> AnalyzeAsync(string inputJson, CancellationToken ct = default);
}

// 当前实现
public class SkAiClient : IAiClient { ... }  // Semantic Kernel + 豆包
public class DoubaoAiClient : IAiClient { ... }  // 直接 HTTP 调用

// 可扩展
public class OpenAiClient : IAiClient { ... }
public class DeepSeekClient : IAiClient { ... }
```

通过 DI 容器切换模型实现，无需修改业务代码。

### 10.2 RAG 增强（历史案例检索）

在上下文构建阶段增加一步：

```
AiWorker.ProcessOne
  │
  ├─ 构建错误快照
  ├─ 查询日志时间线
  ├─ 【新增】RAG: 从知识库检索相似错误案例
  │    ├─ 向量化错误消息 + 异常类型 → 检索 Top-3 历史案例
  │    └─ 附加到 User Prompt 作为参考
  └─ 调用 LLM
```

需要新增：
- `IKnowledgeRepository` — 存储历史 AIAnalysis 记录
- `VectorSearchService` — 语义检索（Embedding）

### 10.3 自动修复 Pipeline

```
LLM 输出修复建议（自然语言）
        ↓
CodeFixParser 解析建议
  ├─ 代码修复建议 → CodeFixGenerator → PR
  ├─ 配置修复建议 → ConfigUpdater → 配置变更
  └─ 基础设施建议 → TicketCreator → 工单
```

### 10.4 监控与效果评估

| 指标 | 来源 | 用途 |
|------|------|------|
| 分析准确率 | 人工验证 | 评估模型质量 |
| 处理延迟(P50/P99) | AiTask.CreatedAt → ProcessedAt | 性能监控 |
| 重试率 | AiTask.Attempts > 0 | 质量监控 |
| DeadLetter 率 | Status = DeadLetter | 异常监控 |
| Token 消耗 | OutputJson 长度 | 成本监控 |

### 10.5 模型微调方向

- 积累 `(InputJson, OutputJson)` 对 → 微调专用模型
- 构建领域语料（当前项目的错误模式）
- 减少 prompt 长度，降低 Token 消耗

---

## 11. 数据一致性保证

### 11.1 事务边界

```
AiWorker.ProcessOne
├─ AiTaskService.TakeBatchAsync   ← 无事务（允许并发抢锁）
├─ LLM 调用                        ← 无事务（外部 HTTP 调用）
├─ AiTaskService.MarkCompletedAsync ← 单行 UPDATE（隐式事务）
└─ 写入 AIAnalysis 表              ← 单行 INSERT（隐式事务）
```

### 11.2 幂等性

- `MarkCompletedAsync` 按 Id 更新，重复调用仅生效一次
- `MarkFailedAsync` 按 Id 更新，Attempts 自增

### 11.3 多实例一致性

```
场景：实例A 和 实例B 同时处理同一个 AiTask
  │
  ├─ TakeBatchAsync：两实例都 SELECT 到该任务
  ├─ UPDATE Lock：行锁保证只有一个 UPDATE 成功
  ├─ UPDATE 失败的实例：该任务不在其 ids 列表中
  └─ 结果：该任务只被一个实例处理
```

---

## 12. 安全与审计

| 需求 | 方案 |
|------|------|
| 输入脱敏 | AiAnalysisInputDto 中不包含敏感字段（密码、Token） |
| 日志审计 | AiTask 表记录完整处理链路（创建、锁定、处理、完成） |
| 数据追溯 | AIAnalysis 存储 InputJson / OutputJson 原始数据 |
| 多实例审计 | LockedBy 字段记录 Worker 实例名（Environment.MachineName） |

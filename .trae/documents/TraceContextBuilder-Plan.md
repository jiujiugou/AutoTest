# Trace Context Builder — 为 LLM 推理优化的 trace 表达层

## 目标

构建一个 **TraceContextBuilder**，作为"为 LLM 推理优化的 trace 表达层"，将其集成到 `KernelFactory.Create()` 中，使 Semantic Kernel 在调用 LLM 进行日志分析和错误建议时，能够提供丰富的 trace 上下文数据。

## 涉及的文件与行号

| 文件 | 行号 | 内容 |
|------|------|------|
| `AiAnalysisConsumer.cs` | 24 | `internal class AiAnalysisConsumer : INotificationHandler<MonitorExecutionFailedEvent>` |
| `LogService.cs` | 13 | `public sealed class LogService : ILogService` |
| `LogService.cs` | 155 | `public async Task<List<ElasticLogDocument>> GetAiErrorContextAsync(...)` |
| `KernelFactory.cs` | 8 | `public static class KernelFactory` |

## 架构设计

```
┌──────────────────────────────────────────────────────────────┐
│                    Semantic Kernel (LLM)                      │
│  ┌────────────────────────────────────────────────────────┐  │
│  │  AutoTestPlugin (monitor_list/monitor_run/monitor_stats)│  │
│  │  + TracePlugin (trace_context) ← 新增                  │  │
│  └────────────────────────────────────────────────────────┘  │
│                              ↕                               │
│  ┌────────────────────────────────────────────────────────┐  │
│  │  TraceContextBuilder (LLM-optimized trace expression)   │  │
│  │  - BuildTraceContextAsync() → Markdown                  │  │
│  └────────────────────────────────────────────────────────┘  │
│                              ↕                               │
│  ┌────────────────────────────────────────────────────────┐  │
│  │  ILogService.GetAiErrorContextAsync()                   │  │
│  │  → Elasticsearch: traceId + level(Error/Fatal) + time  │  │
│  └────────────────────────────────────────────────────────┘  │
└──────────────────────────────────────────────────────────────┘
```

## 实施步骤

### Step 1: 在 `AutoTest.Core.AI` 中定义 TraceLogEntry DTO

- **文件**: `d:\AutoTest\src\AutoTest.Core\AI\TraceLogEntry.cs`
- **内容**: 轻量 DTO，包含 Timestamp、Level、Message、Exception 字段
- **原因**: `ElasticLogDocument` 在 Infrastructure 层中，Core 层的接口不应依赖 Infrastructure 的模型

### Step 2: 扩展 `ILogService` 接口

- **文件**: `d:\AutoTest\src\AutoTest.Application\ILogService.cs`
- **变更**: 添加方法 `Task<List<TraceLogEntry>> GetAiErrorContextAsync(string traceId, DateTime? errorTime = null, int windowSeconds = 30, int take = 120)`
- **原因**: 让 AutoTest.AI 能通过接口调用 trace 查询，而不直接依赖 Infrastructure

### Step 3: 在 `LogService` 中实现接口方法

- **文件**: `d:\AutoTest\src\AutoTest.Infrastructure\Log\LogService.cs`
- **变更**: 实现 `ILogService.GetAiErrorContextAsync`，将现有的 `GetAiErrorContextAsync` 返回的 `ElasticLogDocument` 映射为 `TraceLogEntry`
- **注意**: 保持现有方法不动（向后兼容），新增显式接口实现

### Step 4: 创建 `TraceContextBuilder` 类

- **文件**: `d:\AutoTest\src\AutoTest.AI\TraceContextBuilder.cs`
- **命名空间**: `AutoTest.AI`
- **依赖**: `ILogService`（通过构造函数注入）
- **核心方法**:
  - `BuildTraceContextAsync(string traceId, DateTime? errorTime, AiAnalysisInputDto? errorContext = null)`
  - 返回 LLM 优化的 Markdown 格式字符串
  - 包含：TraceId、时间窗口、日志时间线（结构化）、异常信息、失败断言

**LLM-optimized 输出格式示例**:
```markdown
## Trace Context
- **TraceId**: `abc-123`
- **Error Time**: 2026-04-27 10:30:00
- **Time Window**: ±30s

### Log Timeline
| # | Time | Level | Message |
|---|------|-------|---------|
| 1 | 10:29:45 | INFO | Request started |
| 2 | 10:30:00 | ERROR | NullReferenceException ... |
| 3 | 10:30:01 | FATAL | System crash |

### Error Details
- **Type**: NullReferenceException
- **Message**: Object reference not set to ...
- **Stack Trace**: ...
- **Failed Assertions**: ...
```

### Step 5: 在 `KernelFactory.Create()` 中集成 TraceContextBuilder

- **文件**: `d:\AutoTest\src\AutoTest.AI\KernelFactory.cs`
- **变更**:
  - `Create()` 方法改为接受 `ILogService` 参数
  - 内部创建 `TraceContextBuilder` 实例
  - 注册为 Kernel Plugin（`builder.Plugins.AddFromObject<TraceContextBuilder>()`）
  - 使该 Plugin 可通过 `trace_getContext` Kernel Function 被 LLM 调用
- **原因**: 让 LLM 在推理时能按需获取 trace 上下文，作为日志分析和错误建议的数据来源

### Step 6: 注册 DI 服务

- **文件**: `d:\AutoTest\src\AutoTest.Webapi\Program.cs` 或 `d:\AutoTest\src\AutoTest.Infrastructure\AddInfrastructureServiceCollectionExtensions.cs`
- **变更**: 注册 `TraceContextBuilder`（Scoped）到 DI 容器
- **说明**: 由于 `KernelFactory` 需要 `ILogService`，并且 `SkAiClient` 目前在构造函数中直接 new `KernelFactory.Create()`，需要考虑如何注入

### Step 7: 创建 `ITraceContextBuilder` 接口（可选但推荐）

- **文件**: `d:\AutoTest\src\AutoTest.AI\ITraceContextBuilder.cs`
- **目的**: 便于测试和替换实现

## 依赖关系图

```
AutoTest.Core (TraceLogEntry)
    ↑
AutoTest.Application (ILogService + GetAiErrorContextAsync)
    ↑              ↑
AutoTest.AI        AutoTest.Infrastructure
(TraceContextBuilder)  (LogService implements)
    ↑
KernelFactory.Create()
    → registers as Kernel Plugin
    → LLM calls trace_getContext for log analysis & error suggestions
```

## 注意事项

1. **AutoTest.AI 不引用 AutoTest.Infrastructure** — 必须通过 `ILogService` 接口解耦
2. **TraceLogEntry 放在 Core 层** — 让 Application 和 Infrastructure 都能引用
3. **KernelFactory 从静态无参改为带参** — 需要同时更新 `SkAiClient` 中对 `KernelFactory.Create()` 的调用
4. **LLM 优化的关键** — 输出格式要结构化（表格 + 标题 + 关键字段高亮），token 效率优先，避免冗余

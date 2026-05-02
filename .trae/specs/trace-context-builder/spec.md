# Trace Context Builder Spec

## Why
AiAnalysisConsumer 处理执行失败事件时，只传入了异常类型、消息、堆栈和失败断言，缺少完整的 trace 上下文日志。LLM 无法看到错误发生前后的日志时间线，导致日志分析和错误建议不够准确。

## What Changes
- 在 `AutoTest.Core` 新增 `TraceLogEntry` DTO
- 在 `ILogService` 接口新增 `GetAiErrorContextAsync` 方法
- 在 `LogService` 实现该方法
- 新增 `TraceContextBuilder` 类，将 trace 日志格式化为 LLM 优化的 Markdown
- 修改 `KernelFactory.Create()` 接受 `ILogService`，注册 TraceContextBuilder 为 Kernel Plugin
- 在 DI 容器中注册 `TraceContextBuilder`

## Impact
- Affected specs: AI analysis, Kernel plugin registration
- Affected code: KernelFactory.cs, ILogService.cs, LogService.cs, SkAiClient.cs
- New code: TraceLogEntry.cs, TraceContextBuilder.cs

## ADDED Requirements

### Requirement: Trace Context Builder
系统应提供一个 TraceContextBuilder，能够从 Elasticsearch 拉取指定 traceId 的错误上下文日志，并格式化为 LLM 友好的 Markdown 格式。

#### Scenario: 构建错误 trace 上下文
- **WHEN** 调用 `BuildTraceContextAsync` 传入 traceId 和 errorTime
- **THEN** 返回包含 TraceId、时间窗口、日志时间线表格、错误详情的 Markdown 字符串

### Requirement: Kernel Plugin Integration
KernelFactory 应将 TraceContextBuilder 注册为 Semantic Kernel Plugin，使 LLM 能按需获取 trace 上下文。

#### Scenario: LLM 调用 trace 查询
- **WHEN** LLM 在分析日志时需要 trace 上下文
- **THEN** 可通过 `trace_getContext` Kernel Function 获取格式化的 trace 数据

## MODIFIED Requirements

### Requirement: ILogService
ILogService 新增 `GetAiErrorContextAsync` 方法，返回 `List<TraceLogEntry>`。

### Requirement: KernelFactory.Create
KernelFactory.Create() 从静态无参改为接受 `ILogService` 参数，内部注册 TraceContextBuilder 插件。

### Requirement: SkAiClient
SkAiClient 构造函数需传入 `ILogService`，以便正确创建 Kernel。

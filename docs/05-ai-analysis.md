# AI 分析模块（核心）

## 5.1 模块定位

AI 分析模块是 AutoTest 的核心差异化能力。它在执行链路检测到失败后，自动聚合跨服务日志、调用 LLM 进行推理、输出结构化的根因分析和修复建议。

## 5.2 模块组件

```
AutoTest.AI/                     # Semantic Kernel 集成层
├── KernelFactory.cs             # SK Kernel 构建（模型/API Key/Plugin 注册）
├── SkAiClient.cs                # LLM 调用封装（AnalyzeAsync）
├── TraceContextBuilder.cs       # Semantic Kernel Plugin（ES 日志查询工具）
└── AutoTestPlugin.cs            # Plugin 注册入口

AutoTest.Infrastructure/AI/      # 基础设施层（任务调度 + Prompt）
├── AiWorker.cs                  # BackgroundService，轮询 AiTask 队列
├── AiTaskService.cs             # AiTask CRUD + 状态管理
├── AiAnalysisPromptBuilder.cs   # SystemPrompt + UserPrompt 构建
├── AutoRecoveryService.cs       # 自动恢复执行器
└── RecoverySuggestionParser.cs  # LLM 建议解析→结构化恢复动作

AutoTest.Core/AI/                # 领域模型
├── AiTask.cs                    # 任务队列实体
├── AIAnalysis.cs                # 分析结果实体
├── AiAnalysisInputDto.cs        # LLM 输入 DTO
├── AiAnalysisOutputDto.cs       # LLM 输出 DTO（结构化 JSON Schema）
├── AutoRecoveryRecord.cs        # 自动恢复记录
├── RecoveryActionType.cs        # 恢复动作枚举
├── TraceLogEntry.cs             # 日志条目 DTO
├── IAiClient.cs                 # LLM 客户端接口
├── IAutoRecoveryService.cs      # 恢复服务接口
└── IAnalysisRepository.cs       # 分析结果仓储接口
```

## 5.3 执行流程

```
AiAnalysisConsumer (收到失败事件)
    │
    ├── 构建 AiAnalysisInputDto (Exception, StackTrace, TraceId, Assertions)
    ▼
AiTaskService.EnqueueAsync() → INSERT AiTask(Status=Pending)
    │
    ▼ (轮询)
AiWorker.ProcessOne()
    │
    ├── Step 1: 反序列化 InputJson
    ├── Step 2: 从 ES 拉取日志时间线（按 traceId）
    ├── Step 3: 构建 Prompt（SystemPrompt + UserPrompt）
    ├── Step 4: 调用 SkAiClient.AnalyzeAsync() → LLM 推理
    ├── Step 5: 解析 JSON → AiAnalysisOutputDto
    ├── Step 6: 落库 AIAnalysis
    ├── Step 7: 触发 AutoRecoveryService.ExecuteRecoveryAsync()
    └── Step 8: MarkCompletedAsync()
```

## 5.4 Prompt 设计

### SystemPrompt

定义 LLM 的角色定位、分析规则和输出约束：

```
你是一个严谨的分布式系统故障分析专家...
分析规则：
  1. 先读错误快照，理解错误类型和代码位置
  2. 再读日志时间线，注意日志来自多个服务
  3. 按时间顺序追踪错误传播链路
分布式故障模式：
  - 级联超时：多个服务依次超时 → 根因在最下游
  - 级联崩溃：异常从上游扩散到下游
  - 资源耗尽：连接池/OOM → 根因是请求堆积
```

### UserPrompt 结构

```
## 错误快照
- 异常类型: NullReferenceException
- 错误消息: Object reference not set...
- Trace ID: 550e8400-e29b-...
- 堆栈信息: ...

## 日志时间线（±30秒，共15条）
| # | Time | Service | Level | Message |
|---|------|---------|-------|---------|
| 1 | 10:00:01 | api-gateway | ERROR | 上游服务响应超时 |

请基于以上信息分析，按以下 JSON Schema 输出：
{"type":"...", "severity":"...", ...}
```

### Token 预算控制

| 内容 | 最大长度 | 策略 |
|------|---------|------|
| StackTrace | 2048 字符 | 超长截断 + "..." |
| 异常 Message | 1000 字符 | 超长截断 |
| 日志消息 | 150 字符 | 超长截断 |
| `suggestion` | 500 字符 | LLM 输出限制 |
| `rootCause` | 500 字符 | LLM 输出限制 |

## 5.5 LLM 输出 Schema

```json
{
  "type": "TestFailure|ApiError|PerformanceIssue|SecurityIssue",
  "severity": "low|medium|high|critical",
  "category": "NULL_REFERENCE|TIMEOUT|DB_ERROR",
  "summary": "一句话摘要（<100字）",
  "rootCause": "根因分析（<500字）",
  "suggestion": "修复建议（<500字）",
  "impact": "single_request|module_level|system_level",
  "faultService": "触发故障的服务名",
  "confidence": 0.95,
  "errorChain": [
    {"service": "api-gateway", "type": "诱因", "detail": "...边缘情况"},
    {"service": "auth", "type": "故障", "detail": "...异常被抛出"}
  ]
}
```

## 5.6 故障模式识别

| 模式 | 识别特征 | 根因定位策略 |
|------|----------|-------------|
| 级联超时 | 多个服务依次超时 | 查最早出现超时的服务 |
| 级联崩溃 | 异常沿调用链扩散 | 查最上游的异常抛出处 |
| 资源耗尽 | OOM / 连接池满 / Too Many Open Files | 查请求量最高的时段 |
| 网络抖动 | 偶发超时 + 重试成功 | 标记为瞬时故障 |

## 5.7 自动恢复策略

| 恢复动作 | 触发关键词 | 执行方式 | 安全等级 |
|----------|-----------|----------|---------|
| Retry | 重试、retry、重新执行 | 自动重新执行监控任务 | 安全 |
| ConfigFix | 超时、增大、配置 | 记录待人工确认 | 需确认 |
| InfraFix | 扩容、重启、OOM | 记录待人工介入 | 需确认 |
| Notify | 通知、告警、alert | 记录通知日志 | 安全 |

## 5.8 版本演进

- **v1.0**：单服务日志分析，基础错误分类
- **v1.1**：跨服务日志时间线 + Service 列 + 分布式故障模式识别
- **v1.2**（计划）：自动恢复执行链路 + 故障模式库积累

# 架构详解

## 分层架构

```
┌────────────────────────────────────────────────────┐
│                AutoTest.Webapi                     │
│      Controller / SignalR Hub / 中间件              │
├────────────────────────────────────────────────────┤
│            AutoTest.Infrastructure                 │
│  仓储(Dapper) / HostedService / Outbox / AI / 日志   │
├────────────────────────────────────────────────────┤
│            AutoTest.Application                    │
│  Orchestrator / MonitorService / Pipeline / DTO    │
├────────────────────────────────────────────────────┤
│  AutoTest.    AutoTest.     AutoTest.              │
│  Execution    Assertions    Workflow               │
├────────────────────────────────────────────────────┤
│                AutoTest.Core                       │
│     实体 / 接口 / 枚举 / DTO / 领域事件              │
└────────────────────────────────────────────────────┘
```

## 各层职责

### Core — 领域层

纯 C# 类型，零外部依赖。定义：

| 类别 | 说明 |
|------|------|
| 聚合根 | `MonitorEntity`（监控任务 + 状态机）、`ExecutionRecord`（执行记录 + 幂等键 + 心跳）、`TestPlanEntity`（测试计划） |
| 目标 | `HttpTarget` / `TcpTarget` / `DbTarget` / `PythonTarget` / `TemplateTarget` |
| 结果 | `ExecutionResult`（执行结果基类）、`AssertionResult`（断言结果） |
| DSL | `StepSequence`（DAG）、`StepDefinition`、`ParallelGroup`、`DslPipelineContext`、`DslRuntimeContext` |
| 事件 | `MonitorExecutionFailedPayload`（失败事件） |
| 仓储接口 | `IMonitorRepository` / `IExecutionRecordRepository` / `IOutboxRepository` / `ITestPlanRepository` |
| 管线 | `IPipeline` / `IPipelineStep` / `PipelineContext` |
| 执行 | `IExecutionEngine` / `IOrchestrator` |

### Application — 应用层

| 类 | 职责 |
|----|------|
| `Orchestrator` | 执行主流程：管线 → 断言 → 落库 → Outbox |
| `MonitorService` | 监控 CRUD、执行调度、缓存管理 |
| `TestPlanService` | 测试计划 CRUD、批量执行 |
| `TestReportService` | 计划执行报告聚合 |
| `Pipeline` | 管线实现，按反向顺序执行 IPipelineStep |
| `PipelineSelector` | 根据 `Monitor.IsTemplate` 选择 DefaultPipeline 或 TemplatePipeline |
| `ExecutionStep` | 解析执行引擎并执行目标（Template 跳过） |
| `AssertionStep` | 运行断言规则 |
| `ExecutionEngineResolver` | 根据 Target.Type 选择 IExecutionEngine |
| `WorkflowJob` | Hangfire 作业：分布式锁 + 心跳 + Orchestrator |

### Execution — 执行引擎

| 引擎 | 技术 | 关键特性 |
|------|------|---------|
| `HttpExecutionEngine` | Flurl.Http | 认证(Bearer/Basic/ApiKey)、代理、SSL 忽略、重试、限速 |
| `TcpExecutionEngine` | TcpClient + SslStream | TLS、消息收发、连接延迟 |
| `DbExecutionEngine` | ADO.NET | SqlServer/MySQL/PostgreSQL、Query/NonQuery/Scalar |
| `PythonExecutionEngine` | Process | 脚本/文件执行、超时终止、进程树清理 |

每种引擎同时实现 `IExecutionEngine`（管线用）和 DSL 步骤执行器 `IStepExecutor`（模板用）。

### Workflow — DSL 解析与运行时

| 类 | 职责 |
|----|------|
| `DslParser` | JSON → StepSequence，`{{var}}` 和 `{{var:default}}` 变量替换 |
| `DslSchemaValidator` | 校验 steps/parallel 结构合法性 |
| `TemplateResolutionStep` | 管线步骤：解析模板 DSL，写入 DslPipelineContext |
| `WorkflowExecutionStep` | 核心执行引擎：顺序步骤 + 并行组 + 断路器 + 变量提取 + 断言 + 断点续跑 |
| `CircuitBreaker` | 断路器：每目标键，5 次失败 / 60s 窗口 |
| `RedisProgressStore` | 进度持久化：Redis，6h TTL，支持崩溃恢复 |

### Infrastructure — 基础设施

| 组件 | 说明 |
|------|------|
| `MonitorRepository` | Dapper，TargetConfig JSON ↔ MonitorTarget 反序列化 |
| `ExecutionRecordRepository` | 执行记录 CRUD + 统计 + TopN 错误 |
| `TestPlanRepository` | 测试计划 CRUD（MonitorIds JSON 存储） |
| `DapperOutboxRepository` | Outbox 消息写入/锁定/标记 |
| `AiTaskService` | AI 任务队列（乐观锁批处理） |
| `OutboxDispatcherHostedService` | 后台轮询 Outbox → MediatR 分发 |
| `AiWorker` | 后台轮询 AiTask → LLM 调用 → 存储 |
| `ExecutionWatchdogHostedService` | 检测僵死执行（心跳超时 2min / 总时长 15min） |
| `HangfireWorkflowScheduler` | Hangfire 调度封装 |
| `MonitorHub / LogHub` | SignalR 实时推送 |

### CLI — 命令行工具

| 文件 | 说明 |
|------|------|
| `Program.cs` | 入口 + 路由分发 |
| `DslRunner` | 直接执行 DSL JSON 文件，绕过 DB/Orchestrator，走 TemplatePipeline |
| `RunCommand` | `autotest run <file>` — 本地执行 DSL，支持 --json / --var |
| `MonitorRunCommand` | `autotest monitor run <id>` — 执行已持久化监控 |
| `MonitorListCommand` | `autotest monitor list` — 列出监控 |
| `NullProgressStore` | CLI 环境 IProgressStore 空实现 |
| `InProcessLock` | CLI 环境 IDistributedLock 进程内实现 |
| `NullOutboxRepository` | CLI 环境 Outbox 空实现 |

### Auth — 认证授权

| 组件 | 说明 |
|------|------|
| `JwtTokenIssuer` | JWT 签发（sub/role/perm，8h 过期） |
| `DapperAuthService` | 登录/刷新/注销，PBKDF2 密码验证 |
| `Pbkdf2PasswordHasher` | PBKDF2-SHA256, 150k 迭代, 16B 盐 |
| `DapperPermissionStore` | RBAC 权限查询，MemoryCache 缓存 5min |
| `PermissionAuthorizationHandler` | ASP.NET Core 策略授权（`perm:xxx` 格式） |

## 公共组件

### CacheCommons
- `MemoryCacheService` — 防击穿（每 key 一个 SemaphoreSlim），防穿透（缓存空值），TTL 随机抖动

### LockCommons
- `RedisLock` — SET NX EX + 自动续期（TTL/2 间隔）+ Lua 安全释放

---

## 核心数据流

### 普通监控执行

```
Hangfire / 手动触发
  → WorkflowJob: RedisLock → 幂等检查 → 心跳
  → Orchestrator.TryExecuteAsync
      → DefaultPipeline
          → ExecutionStep: IExecutionEngine.ExecuteAsync(target)
          → AssertionStep: AssertionEngine.EvaluateAsync
      → 成功: MarkSuccess + 落库
      → 失败: MarkFailed + 落库 + Outbox(AI分析+钉钉通知)
```

### DSL 模板执行

```
TemplatePipeline:
  TemplateResolutionStep → WorkflowExecutionStep
      → DAG 遍历 Items:
          → StepDefinition: 断路器 → 变量解析 → 执行 → 变量提取 → 断言
          → ParallelGroup: 并行步骤（上下文隔离 + 合并冲突检测）
          → 每步后 SaveProgress → Redis（断点续跑）
      → DslExecutionResultWrapper → 汇总断言
```

### CLI `autotest run`（无 DB）

```
DSL JSON 文件 + --var 参数
  → DslSchemaValidator.Validate
  → IDslParser.ParseAsync → StepSequence (DAG)
  → 构造临时 MonitorEntity (Type="TEMPLATE", 不持久化)
  → PipelineContext.Items 注入 DslPipelineContext
  → IPipeline.ExecuteAsync → TemplatePipeline
      → InProcessLock (单进程) + NullProgressStore (无持久化)
  → 格式化输出 → 退出码 0/1/2
```

### 测试计划执行

```
POST /api/testplan/{id}/run
  → TestPlanService.ExecutePlanAsync: 生成 PlanRunId
  → foreach MonitorId:
      → IMonitorService.TryStartExecutionAsync (planRunId 传入)
      → IOrchestrator.TryExecuteAsync
  → 每条的 ExecutionRecord.PlanRunId = planRunId
  → GET /api/testplan/{id}/report?planRunId= → 聚合报告
```

### AI 分析触发

```
Orchestrator 失败路径 → BuildOutbox(MonitorExecutionFailedPayload)
  → 同一事务内保存
  ↓
OutboxDispatcherHostedService → 轮询领取 → MediatR.Publish
  ↓
AiAnalysisConsumer → TargetSummaryBuilder → 构建 Prompt
  → LLM 调用 → 存储 AIAnalysis → 钉钉 Webhook 通知
```

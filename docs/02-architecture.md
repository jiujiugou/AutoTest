# 系统架构

## 2.1 分层架构

AutoTest 采用典型的 DDD 分层架构，自下而上分为四层：

```
┌─────────────────────────────────────────────────────────┐
│                    Webapi （接口层）                      │
│  Controllers / SignalR Hubs / JWT Auth / Middleware      │
├─────────────────────────────────────────────────────────┤
│                 Application （应用层）                     │
│  MonitorService / Orchestrator / AssertionEngine         │
│  IAiTaskService / IWorkflowScheduler                     │
│  Pipeline（Execution → Assertion → Persistence → Event） │
├─────────────────────────────────────────────────────────┤
│                  Core （领域层）                          │
│  MonitorEntity / ExecutionRecord / AIAnalysis / AiTask   │
│  OutboxMessage / AssertionResult / 接口定义              │
├─────────────────────────────────────────────────────────┤
│             Infrastructure （基础设施层）                  │
│  Repositories(Dapper) / AI / Logging(Serilog+ES)         │
│  Hangfire / OutboxDispatcher / HostedServices            │
│  Cache / Redis / SignalR Hub                             │
└─────────────────────────────────────────────────────────┘
```

### 2.1.1 各层职责

| 层 | 职责 | 关键约束 |
|----|------|----------|
| **Webapi** | HTTP 入口、认证授权、SignalR 实时推送、Swagger 文档 | 不包含业务逻辑 |
| **Application** | 用例编排（CRUD + 执行 + 断言 + 事件发布） | 依赖 Core 接口，不依赖 Infrastructure |
| **Core** | 领域模型、聚合根、仓储接口、事件定义 | 零外部依赖 |
| **Infrastructure** | 数据库读写、AI 推理、日志写入、后台任务 | 实现 Core 中定义的接口 |

## 2.2 架构风格

### CQRS 读/写分离（轻度）

- **写操作**：`MonitorService` → `IMonitorRepository` → Dapper → DB
- **读操作**：`IDashboardService` + `ILogService` 直接查询 ES / 数据库聚合
- **事件驱动**：写操作完成后通过 Outbox + MediatR 触发下游（如 AiAnalysisConsumer）

### Outbox Pattern（事件可靠性）

所有领域事件先写入 `OutboxMessage` 表（与业务操作同一事务），再由 `OutboxDispatcherHostedService` 轮询发送到 MediatR Handler，确保事件不丢失。

### 异步 Worker 模式

AI 分析任务通过 `AiTask` 表作为队列，`AiWorker` (BackgroundService) 轮询消费，支持：
- 批量抢任务（DB 乐观锁）
- 并发控制（SemaphoreSlim）
- 指数退避重试（5 次 → DeadLetter）

## 2.3 分布式全景

```
                        ┌───────────────┐
                        │   Serilog     │
                        │  + Elastic    │
                        │  (集中日志)    │
                        └───────┬───────┘
                                │ 写入
        ┌──────────┐   ┌───────┴───────┐   ┌──────────┐
        │ Hangfire │   │   AutoTest    │   │   Redis  │
        │ (调度器)  │──▶│  Webapi +     │──▶│ (缓存)   │
        └──────────┘   │  Worker       │   └──────────┘
                       └───────┬───────┘
                               │
                ┌──────────────┼──────────────┐
                │              │              │
         ┌──────▼─────┐ ┌─────▼──────┐ ┌─────▼──────┐
         │   SQL DB   │ │    ES      │ │  LLM API   │
         │ (业务数据)  │ │ (日志存储)  │ │ (豆包/OAI) │
         └────────────┘ └────────────┘ └────────────┘
```

### 2.3.1 关键设计决策

| 决策 | 选择 | 理由 |
|------|------|------|
| 任务队列 | AiTask 表（DB 轮询） | 无需引入 Kafka/RabbitMQ，降低运维复杂度 |
| 日志存储 | Elasticsearch | 全文检索、时间范围查询、多维度聚合 |
| AI 框架 | Semantic Kernel | 插件化、支持多模型 Provider |
| 调度器 | Hangfire | 持久化、去重、重试、监控面板 |
| 事件可靠性 | Outbox Pattern | 保证事件至少一次投递 |

## 2.4 项目模块结构

```
src/
├── AutoTest.Webapi/           # ASP.NET Core Web 入口
├── AutoTest.Application/      # 应用层（用例编排）
├── AutoTest.Core/             # 领域层（模型+接口）
├── AutoTest.Infrastructure/   # 基础设施（仓储/AI/日志/变量解析）
├── AutoTest.Execution/        # 执行引擎（HTTP/TCP/DB/Python）+ IStepExecutor 适配器
├── AutoTest.Assertions/       # 断言引擎
├── AutoTest.Dsl/              # DSL 解析（Schema 校验 + 变量替换 + DAG 构建）
├── AutoTest.Orchestration/    # Runtime 编排（步骤调度 + 重试/降级 + 断路器）
├── AutoTest.AI/               # AI 集成（Semantic Kernel）
├── AutoTest.Migrations/       # FluentMigrator 数据库迁移
├── Auth/                      # RBAC 认证授权模块
├── common/
│   ├── CacheCommons/          # 缓存抽象（防穿透/防击穿/防雪崩）
│   ├── LockCommons/           # 分布式锁抽象 + Redis 实现
│   └── EventCommons/          # 事件抽象（MediatR）
└── OutBox/                    # Outbox 调度器（独立模块，待清理）
```

# AutoTest（项目文档）

AutoTest 是一个“监控/自动化执行”系统：通过配置 Monitor（目标 + 断言 + 调度），周期性或手动触发执行，落库执行记录与断言结果，并在失败时通过 Outbox Webhook 推送通知；前端实时展示运行状态与历史执行结果。

## 项目结构

- 后端（.NET）
  - [AutoTest.Webapi](file:///d:/AutoTest/src/AutoTest.Webapi)：HTTP API、认证与权限、SignalR Hub、启动与配置
  - [AutoTest.Application](file:///d:/AutoTest/src/AutoTest.Application)：应用层服务（MonitorService）、执行编排（Orchestrator）、Pipeline
  - [AutoTest.Core](file:///d:/AutoTest/src/AutoTest.Core)：领域模型（Monitor/Execution/Outbox/Target/Assertion）与仓储接口
  - [AutoTest.Infrastructure](file:///d:/AutoTest/src/AutoTest.Infrastructure)：Dapper 仓储、Hangfire 调度、Redis 锁、Outbox 分发器、Watchdog
  - [AutoTest.Execution](file:///d:/AutoTest/src/AutoTest.Execution)：执行引擎（HTTP/TCP/DB/Python）
  - [AutoTest.Assertions](file:///d:/AutoTest/src/AutoTest.Assertions)：断言引擎（HTTP/TCP/DB/Python）
  - [AutoTest.Migrations](file:///d:/AutoTest/src/AutoTest.Migrations)：FluentMigrator 迁移与初始化数据
  - [Auth](file:///d:/AutoTest/src/Auth)：RBAC 与鉴权扩展（用户/角色/权限/策略）
  - 公共库：CacheCommons / EventCommons（见 [common](file:///d:/AutoTest/src/common)）
- 前端（Vue + Vite）
  - [AutoTestWeb](file:///d:/AutoTest/AutoTestWeb)：页面（Dashboard/Monitor/Task/Logs/RBAC/AI）、API 封装、SignalR 实时更新
- 测试
  - [AutoTest.test](file:///d:/AutoTest/test/AutoTest.test)：集成测试与单元测试（SQLite/内存等）

## 核心流程

### 1. Monitor 配置

一个 Monitor 由三部分组成：
- Target：要执行的目标（HTTP/TCP/DB/Python）
- Assertions：断言规则（对执行结果进行校验）
- Schedule：调度配置（每日执行/次数上限等）

监控任务模型：见 [MonitorEntity](file:///d:/AutoTest/src/AutoTest.Core/Monitor/MonitorEntity.cs)。

### 2. 调度与执行编排

执行路径（简化）：
- 调度入口：Hangfire Job → [WorkflowJob](file:///d:/AutoTest/src/AutoTest.Infrastructure/WorkflowJob.cs)
- 应用服务：幂等检查 + Running 落库 + 预创建执行记录 → [MonitorService.TryStartExecutionAsync](file:///d:/AutoTest/src/AutoTest.Application/MonitorService.cs)
- 执行编排：Pipeline 执行 + 断言 + 落库 + Outbox → [Orchestrator](file:///d:/AutoTest/src/AutoTest.Application/Orchestrator.cs)
- 实时推送：SignalR 推送运行中/完成事件 → [MonitorHub](file:///d:/AutoTest/src/AutoTest.Infrastructure/Hubs/MonitorHub.cs)

### 3. 执行态、幂等与可恢复（抗崩溃）

为了避免“崩溃后状态丢失/重复执行/卡死”：
- 执行开始即落库：插入一条 Running 的 ExecutionRecord（`FinishedAt=NULL`），结束后更新同一条记录为最终状态
- 心跳：执行过程中周期性更新 `HeartbeatAtUtc`（用于卡死判定）
- Watchdog：后台扫描 Running 且心跳过期的执行，标记 Timeout 并复位 Monitor 状态
- 幂等：支持 `Idempotency-Key`，相同 key 不重复执行

相关代码与迁移：
- 迁移：新增 `IdempotencyKey/LockedBy/HeartbeatAtUtc` → [AddExecutionRecordRuntimeColumns](file:///d:/AutoTest/src/AutoTest.Migrations/AddExecutionRecordRuntimeColumns.cs)
- 心跳写入：`UpdateHeartbeatAsync` → [ExecutionRecordRepository](file:///d:/AutoTest/src/AutoTest.Infrastructure/ExecutionRecordRepository.cs)
- Watchdog：→ [ExecutionWatchdogHostedService](file:///d:/AutoTest/src/AutoTest.Infrastructure/ExecutionWatchdogHostedService.cs)

### 4. Outbox Webhook（可靠通知）

失败或异常时写入 OutboxMessage，后台服务轮询并发送 Webhook：
- Outbox 仓储（Dapper）：→ [DapperOutboxRepository](file:///d:/AutoTest/src/AutoTest.Infrastructure/Outbox/DapperOutboxRepository.cs)
- 分发器：→ [OutboxWebhookDispatcherHostedService](file:///d:/AutoTest/src/AutoTest.Infrastructure/Outbox/OutboxWebhookDispatcherHostedService.cs)

已修复的关键点：锁过期的 `Processing` 消息可重新认领，避免 dispatcher 崩溃导致永久卡死。

## 运行方式

### 后端（AutoTest.Webapi）

在仓库根目录执行：

```powershell
dotnet run --project D:\AutoTest\src\AutoTest.Webapi\AutoTest.Webapi.csproj
```

数据库与迁移：
- 默认通过 FluentMigrator 在启动期运行迁移（配置见 [Program.cs](file:///d:/AutoTest/src/AutoTest.Webapi/Program.cs) / [MigrationServiceCollectionExtensions](file:///d:/AutoTest/src/AutoTest.Migrations/MigrationServiceCollectionExtensions.cs)）
- 支持 `SqlServer` 与 `Sqlite`（由 `Database:Provider` 控制）

### 前端（AutoTestWeb）

1) 先启动后端（前端默认代理到 `http://localhost:5033`）

2) 启动前端

```bash
cd AutoTestWeb
npm install
npm run dev
```

如果后端不是 5033 端口，启动前端前设置：

```powershell
$env:VITE_API_BASE_URL="http://localhost:5033"
```

开发环境通过 Vite proxy 把 `/api/*` 代理到后端，因此前端统一请求 `/api/...`。

## 配置说明（常用）

后端配置来源：`appsettings.json` / `appsettings.Development.json` / 环境变量。

### 数据库

- `Database:Provider`：`SqlServer` 或 `Sqlite`
- `ConnectionStrings:DefaultConnection`：业务数据库连接串（Monitor/Execution/Outbox 等）
- `ConnectionStrings:HangfireConnection`：Hangfire 存储连接串

注意：
- SQLite 示例连接串可包含 `Foreign Keys=True;`，Hangfire 会自动按 provider 选择 SQLite 或 SqlServer 存储。

### Outbox Webhook

配置节点：`Outbox:Webhook`
- `Enabled`：是否启用
- `Url`：接收地址
- 其余参数见 [OutboxWebhookOptions](file:///d:/AutoTest/src/AutoTest.Infrastructure/Outbox/OutboxWebhookOptions.cs)

### AI

AI 入口 API：`POST /api/AiAgent/chat`（见 [AiAgentController](file:///d:/AutoTest/src/AutoTest.Webapi/Controllers/AiAgentController.cs)）

## API 速览（前端已对接）

- 认证：`/api/auth/login`、`/api/auth/refresh`、`/api/auth/bootstrap`、`/api/auth/logout`
- 监控：`/api/monitor/*`
  - 手动触发：`POST /api/monitor/{id}/run`
  - 幂等：支持 Header `Idempotency-Key`（不传则服务端返回一个生成的 key）
- RBAC：`/api/rbac/*`
- 日志：`/api/logs/*`
- AI：`/api/AiAgent/chat`

## 测试与构建

```powershell
dotnet build D:\AutoTest\AutoTest.sln
dotnet test D:\AutoTest\test\AutoTest.test\AutoTest.test.csproj
```

前端构建：

```bash
cd AutoTestWeb
npm run build
```

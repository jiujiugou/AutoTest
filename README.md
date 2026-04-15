# AutoTest（项目文档）

AutoTest 是一个“监控/自动化执行”系统：通过配置 Monitor（目标 + 断言 + 调度），周期性或手动触发执行，落库执行记录与断言结果，并在失败时通过 Outbox Webhook 推送通知；前端实时展示运行状态与历史执行结果。

## 项目结构

- 后端（.NET）
  - [`src/AutoTest.Webapi`](src/AutoTest.Webapi)：HTTP API、认证与权限、SignalR Hub、启动与配置
  - [`src/AutoTest.Application`](src/AutoTest.Application)：应用层服务（MonitorService）、执行编排（Orchestrator）、Pipeline
  - [`src/AutoTest.Core`](src/AutoTest.Core)：领域模型（Monitor/Execution/Outbox/Target/Assertion）与仓储接口
  - [`src/AutoTest.Infrastructure`](src/AutoTest.Infrastructure)：Dapper 仓储、Hangfire 调度、Redis 锁、Outbox 分发器、Watchdog
  - [`src/AutoTest.Execution`](src/AutoTest.Execution)：执行引擎（HTTP/TCP/DB/Python）
  - [`src/AutoTest.Assertions`](src/AutoTest.Assertions)：断言引擎（HTTP/TCP/DB/Python）
  - [`src/AutoTest.Migrations`](src/AutoTest.Migrations)：FluentMigrator 迁移与初始化数据
  - [`src/Auth`](src/Auth)：RBAC 与鉴权扩展（用户/角色/权限/策略）
  - 公共库：CacheCommons / EventCommons（见 [`src/common`](src/common)）
- 前端（Vue + Vite）
  - [`AutoTestWeb`](AutoTestWeb)：页面（Dashboard/Monitor/Task/Logs/RBAC/AI）、API 封装、SignalR 实时更新
- 测试
  - [`test/AutoTest.test`](test/AutoTest.test)：集成测试与单元测试（SQLite/内存等）

## 核心流程

### 1. Monitor 配置

一个 Monitor 由三部分组成：
- Target：要执行的目标（HTTP/TCP/DB/Python）
- Assertions：断言规则（对执行结果进行校验）
- Schedule：调度配置（每日执行/次数上限等）

监控任务模型：见 [`src/AutoTest.Core/Monitor/MonitorEntity.cs`](src/AutoTest.Core/Monitor/MonitorEntity.cs)。

### 2. 调度与执行编排

执行路径（简化）：
- 调度入口：Hangfire Job → [`src/AutoTest.Infrastructure/WorkflowJob.cs`](src/AutoTest.Infrastructure/WorkflowJob.cs)
- 应用服务：幂等检查 + Running 落库 + 预创建执行记录 → [`src/AutoTest.Application/MonitorService.cs`](src/AutoTest.Application/MonitorService.cs)
- 执行编排：Pipeline 执行 + 断言 + 落库 + Outbox → [`src/AutoTest.Application/Orchestrator.cs`](src/AutoTest.Application/Orchestrator.cs)
- 实时推送：SignalR 推送运行中/完成事件 → [`src/AutoTest.Infrastructure/Hubs/MonitorHub.cs`](src/AutoTest.Infrastructure/Hubs/MonitorHub.cs)

### 3. 执行态、幂等与可恢复（抗崩溃）

为了避免“崩溃后状态丢失/重复执行/卡死”：
- 执行开始即落库：插入一条 Running 的 ExecutionRecord（`FinishedAt=NULL`），结束后更新同一条记录为最终状态
- 心跳：执行过程中周期性更新 `HeartbeatAtUtc`（用于卡死判定）
- Watchdog：后台扫描 Running 且心跳过期的执行，标记 Timeout 并复位 Monitor 状态
- 幂等：支持 `Idempotency-Key`，相同 key 不重复执行

相关代码与迁移：
- 迁移：新增 `IdempotencyKey/LockedBy/HeartbeatAtUtc` → [`src/AutoTest.Migrations/AddExecutionRecordRuntimeColumns.cs`](src/AutoTest.Migrations/AddExecutionRecordRuntimeColumns.cs)
- 心跳写入：`UpdateHeartbeatAsync` → [`src/AutoTest.Infrastructure/ExecutionRecordRepository.cs`](src/AutoTest.Infrastructure/ExecutionRecordRepository.cs)
- Watchdog：→ [`src/AutoTest.Infrastructure/ExecutionWatchdogHostedService.cs`](src/AutoTest.Infrastructure/ExecutionWatchdogHostedService.cs)

### 4. Outbox Webhook（可靠通知）

失败或异常时写入 OutboxMessage，后台服务轮询并发送 Webhook：
- Outbox 仓储（Dapper）：→ [`src/AutoTest.Infrastructure/Outbox/DapperOutboxRepository.cs`](src/AutoTest.Infrastructure/Outbox/DapperOutboxRepository.cs)
- 分发器：→ [`src/AutoTest.Infrastructure/Outbox/OutboxWebhookDispatcherHostedService.cs`](src/AutoTest.Infrastructure/Outbox/OutboxWebhookDispatcherHostedService.cs)

已修复的关键点：锁过期的 `Processing` 消息可重新认领，避免 dispatcher 崩溃导致永久卡死。

## 运行方式

### 后端（AutoTest.Webapi）

在仓库根目录执行：

```powershell
dotnet run --project src/AutoTest.Webapi/AutoTest.Webapi.csproj
```

数据库与迁移：
- 默认通过 FluentMigrator 在启动期运行迁移
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

### AI

AI 入口 API：`POST /api/AiAgent/chat`

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
dotnet build AutoTest.sln
dotnet test test/AutoTest.test/AutoTest.test.csproj
```

前端构建：

```bash
cd AutoTestWeb
npm run build
```

## Docker 部署

项目根目录已提供可直接上线的单机 Docker 编排，包含：
- `web`：前端 Nginx 静态站点
- `api`：ASP.NET Core WebApi
- `sqlserver`：业务库与 Hangfire 库
- `redis`：分布式锁 / 幂等
- `elasticsearch`：日志历史查询

### 1. 准备环境变量

在服务器仓库根目录复制一份环境变量文件：

```bash
cp .env.example .env
```

至少修改这两个值：
- `AUTOTEST_SQL_SA_PASSWORD`
- `AUTOTEST_JWT_SIGNING_KEY`

### 2. 一键启动

```bash
docker compose up -d --build
```

### 3. 访问地址

- 前端首页：`http://你的服务器IP/`
- 后端 API：`http://你的服务器IP/api/...`
- SignalR：`http://你的服务器IP/hubs/...`
- Elasticsearch：`http://你的服务器IP:9200`

### 4. 默认端口映射

- `80 -> web`
- `5033 -> api`
- `1433 -> sqlserver`
- `6379 -> redis`
- `9200 -> elasticsearch`

### 5. 容器内关键配置

- 业务数据库：`Server=sqlserver;Database=AutoTestDb;...`
- Hangfire 数据库：`Server=sqlserver;Database=HangfireDb;...`
- Redis：`redis:6379`
- Elasticsearch：`http://elasticsearch:9200`

### 6. 注意事项

- 首次启动会先执行 `sqlserver-init`，自动创建 `AutoTestDb` 和 `HangfireDb`
- API 启动时会自动执行 FluentMigrator 迁移
- 当前编排默认关闭 Elasticsearch 安全认证，适合单机自托管；如果后续要暴露到公网，建议只开放 `80`，并在安全组中限制 `1433/6379/9200`
- 当前前端 Nginx 已包含 `/api` 与 `/hubs` 反向代理，以及 WebSocket 升级头

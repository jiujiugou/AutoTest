# AutoTest — 自动化监控平台

> 定时检测 HTTP/TCP/DB/业务流程，失败钉钉通知 + AI 诊断。一行命令部署。

## 快速开始

### 1. 部署（1 分钟）

```bash
cp .env.example .env
# 编辑 .env 填 AUTOTEST_JWT_SIGNING_KEY 和 SQL 密码
docker compose up -d
```

### 2. 登录并创建第一个监控（1 分钟）

1. 打开 `http://你的服务器IP`
2. 用你设的管理员账号密码登录
3. 进入「任务调度」页，点「导入」→ 选「API 健康检查」→ 填你的 API 地址 → 确定

第一个监控已经在跑了。

### 3. 收到钉钉通知（2 分钟）

编辑 `.env` 里 `Outbox__Webhook__Url` 改成你的钉钉机器人 Webhook 地址，重启：

```bash
docker compose up -d
```

手动执行一次刚才创建的监控，群里应该收到一张通知卡片。

### 4. 下一步

- 浏览「导入」菜单看更多预置监控模板
- 读 `docs/AutoTest推广与产品化建议.md` 了解产品规划
- 读 `docs/` 下设计文档了解 DSL 模板引擎（多步骤业务流程监控）

## 常见监控场景（点一下就导入）

| 我要监控 | 用什么 |
| -------- | ------ |
| API 掉线就通知 | HTTP 健康检查 |
| Redis/MySQL 不通就告警 | TCP 端口探测 |
| 订单数异常再通知 | DB 行数检查 |
| 登录→查订单→验金额一条链路 | TEMPLATE 模板 |
| 证书还有 N 天过期 | Python 证书检查 |

## 项目结构

- 后端（.NET）
  - [`src/AutoTest.Webapi`](src/AutoTest.Webapi) — HTTP API、认证与权限、SignalR Hub
  - [`src/AutoTest.Application`](src/AutoTest.Application) — MonitorService、执行编排、Pipeline
  - [`src/AutoTest.Core`](src/AutoTest.Core) — 领域模型与仓储接口
  - [`src/AutoTest.Infrastructure`](src/AutoTest.Infrastructure) — Dapper 仓储、Hangfire、Redis 锁、Outbox
  - [`src/AutoTest.Execution`](src/AutoTest.Execution) — 执行引擎（HTTP/TCP/DB/Python）
  - [`src/AutoTest.Assertions`](src/AutoTest.Assertions) — 断言引擎
  - [`src/AutoTest.Migrations`](src/AutoTest.Migrations) — FluentMigrator 迁移
  - [`src/Auth`](src/Auth) — RBAC 与鉴权
- 前端（Vue + Vite）
  - [`AutoTestWeb`](AutoTestWeb) — 页面、API 封装、SignalR 实时更新
- 测试
  - [`test/AutoTest.test`](test/AutoTest.test) — 集成测试与单元测试

## Docker 部署细节

项目根目录已提供可直接上线的单机 Docker 编排：

- `web` — 前端 Nginx 静态站点
- `api` — ASP.NET Core WebApi
- `sqlserver` — 业务库与 Hangfire 库
- `redis` — 分布式锁 / 幂等
- `elasticsearch` — 日志历史查询

### 环境变量

至少修改 `.env` 中这两个值：

- `AUTOTEST_SQL_SA_PASSWORD`
- `AUTOTEST_JWT_SIGNING_KEY`

可选（推荐首次上线用一次）：

- `AUTOTEST_ADMIN_USERNAME`
- `AUTOTEST_ADMIN_PASSWORD`

### 端口映射

| 端口 | 服务 |
| ---- | ---- |
| 80 | Web 前端 |
| 5033 | API |
| 1433 | SQL Server |
| 6379 | Redis |
| 9200 | Elasticsearch |

### 注意事项

- 首次启动会自动创建数据库并执行迁移
- 配置了管理员账号后，API 启动会自动创建，若已有用户则跳过
- 当前默认关闭 ES 安全认证，适合单机自托管；暴露公网建议只开放 80 端口

## 开发环境

```bash
# 后端
dotnet run --project src/AutoTest.Webapi/AutoTest.Webapi.csproj

# 前端（另一个终端）
cd AutoTestWeb
npm install
npm run dev
```

前端开发默认代理到 `http://localhost:5033`，可通过 `VITE_API_BASE_URL` 环境变量修改。

## 测试与构建

```bash
dotnet build AutoTest.sln
dotnet test test/AutoTest.test/AutoTest.test.csproj

# 前端构建
cd AutoTestWeb && npm run build
```

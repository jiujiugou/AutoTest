# AutoTest — 自动化监控与集成测试平台

> 一行命令部署，五分钟上线，失败钉钉通知 + AI 诊断。支持 CI/微服务集成测试与运维巡检。

## 它能做什么

| 类型 | 能力 | 示例 |
|------|------|------|
| HTTP | API 接口健康检查 | 每 5 分钟请求 `/health`，非 200 告警 |
| TCP | 端口可达性探测 | Redis 6379、MySQL 3306 连不上通知 |
| DB | 数据库数据校验 | 订单表行数异常、超时工单告警 |
| Python | 自定义脚本执行 | SSL 证书过期检查、定时任务监控 |
| DSL 模板 | 多步骤业务流程 | 登录 → 查订单 → 验金额，整条链路验证 |

## 核心特性

- **DSL 多步工作流引擎** — JSON 定义步骤编排，支持顺序/并行、变量传递、重试、断路保护
- **多协议断言** — 状态码 / 响应体 / 耗时 / 行数 / 退出码 / Header
- **失败 → AI 分析闭环** — 执行失败自动触发 LLM 根因分析 + 钉钉通知
- **测试计划** — 多个监控归组批量执行，自动生成通过率报告
- **CLI 工具** — `autotest run file.dsl.json` 本地执行，退出码对接 CI/CD
- **RBAC 权限** — JWT + 基于策略的权限控制
- **实时推送** — SignalR 推送监控状态变更

## 快速开始

```bash
# 1. 部署
cp .env.example .env
# 编辑 .env 填 AUTOTEST_JWT_SIGNING_KEY 和数据库密码
docker compose up -d

# 2. 打开浏览器 → 登录 → 一键导入模板 → 创建第一个监控
# 3. 配置钉钉 webhook → 失败自动通知
```

CLI 模式（无需部署服务端）：

```bash
dotnet run --project src/AutoTest.Cli -- run ./tests/workflows/smoke.dsl.json
# 退出码 0 = 全部通过, 1 = 存在失败, 2 = 执行异常
```

## 技术栈

| 层 | 技术 |
|----|------|
| 运行时 | .NET 10 |
| 数据库 | SQL Server / SQLite（Dapper） |
| 缓存 & 锁 | Redis |
| 调度 | Hangfire |
| 实时推送 | SignalR |
| 日志 | Serilog → Elasticsearch |
| AI | OpenAI 兼容 API |
| 迁移 | FluentMigrator |
| 前端 | Vue 3 + Element Plus + Axios |
| 认证 | JWT + PBKDF2-SHA256 + RBAC |

## 项目结构

```
src/
├── AutoTest.Core/           领域模型、接口、枚举
├── AutoTest.Application/    应用服务、编排器、Pipeline、DTO
├── AutoTest.Execution/      HTTP/TCP/DB/Python 执行引擎 + DSL 步骤执行器
├── AutoTest.Assertions/     断言评估器
├── AutoTest.Workflow/       DSL 解析、Schema 校验、DAG 执行、断路、进度持久化
├── AutoTest.Infrastructure/ 仓储(Dapper)、HostedService、Outbox、SignalR
├── AutoTest.Migrations/     FluentMigrator 迁移
├── AutoTest.Webapi/         ASP.NET Core 主机、Controller
├── AutoTest.Cli/            命令行工具（本地 DSL 执行、CI/CD 集成）
├── Auth/                    JWT 签发/验证、RBAC
├── common/CacheCommons/     防击穿 MemoryCache
├── common/LockCommons/      Redis 分布式锁
AutoTestWeb/                 Vue 3 前端
examples/                    DSL 模板示例
```

## 文档索引

| 文档 | 内容 |
|------|------|
| [架构详解](architecture.md) | 分层架构、每层职责、数据流 |
| [数据库设计](database.md) | 表结构、迁移列表 |
| [使用指南](usage-guide.md) | API 参考、DSL 语法、CI/CD 集成 |

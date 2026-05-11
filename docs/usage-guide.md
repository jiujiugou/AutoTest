# 使用指南

## 部署

### Docker 部署（推荐）

```bash
cp .env.example .env
# 编辑 .env: AUTOTEST_JWT_SIGNING_KEY (≥32字符), 数据库密码
docker compose up -d
# 访问 http://localhost
```

首次启动自动运行数据库迁移。默认管理员账号 `admin`，密码在 .env 中配置。

### 开发环境

```bash
# 后端
cd src/AutoTest.Webapi
dotnet run
# API 运行在 http://localhost:5033

# 前端
cd AutoTestWeb
npm install && npm run dev
# 前端运行在 http://localhost:5173
```

---

## CLI 工具

### 安装

```bash
dotnet build src/AutoTest.Cli -c Release
# 可执行文件: src/AutoTest.Cli/bin/Release/net10.0/autotest
```

### 命令

#### `autotest run` — 直接执行 DSL 文件

```bash
# 基础用法
autotest run ./workflow.dsl.json

# 传入变量
autotest run ./workflow.dsl.json --var baseUrl=https://api.example.com --var token=abc123

# JSON 输出（CI 友好）
autotest run ./workflow.dsl.json --json

# 自定义超时
autotest run ./workflow.dsl.json --timeout 120

# 退出码: 0=全部通过, 1=存在失败, 2=执行异常
```

#### `autotest monitor run` — 执行已持久化监控

```bash
autotest monitor run <monitor-guid>
autotest monitor run <monitor-guid> --json
```

#### `autotest monitor list` — 列出监控

```bash
autotest monitor list
autotest monitor list --json
autotest monitor list --take 20
```

### CI/CD 集成

GitHub Actions：

```yaml
- run: dotnet run --project src/AutoTest.Cli -- run ./tests/workflows/smoke.dsl.json --json
```

完整示例见 [.github/workflows/autotest.yml](../.github/workflows/autotest.yml)。

---

## API 参考

所有接口需 JWT Bearer Token。权限格式：`perm:resource.action`。

### Monitor — `/api/monitor`

| 方法 | 路由 | 权限 | 说明 |
|------|------|------|------|
| GET | `/list` | `perm:api.monitor.view` | 监控列表 |
| GET | `/{id}` | `perm:api.monitor.view` | 单个详情 |
| POST | `/` | `perm:api.monitor.create` | 创建 |
| PUT | `/{id}` | `perm:api.monitor.update` | 更新 |
| DELETE | `/{id}` | `perm:api.monitor.delete` | 删除 |
| POST | `/{id}/run` | `perm:api.monitor.run` | 立即执行 |
| GET | `/{id}/executions` | `perm:api.monitor.view` | 执行记录 |
| GET | `/{id}/executions/latest` | `perm:api.monitor.view` | 最新记录 |
| GET | `/executions/{execId}/assertions` | `perm:api.monitor.view` | 断言结果 |
| GET | `/{id}/runtime-stats` | `perm:api.monitor.view` | 运行统计 + TopN 错误 |
| GET | `/executions/{execId}/analysis` | `perm:api.analysis.read` | AI 分析 |

### TestPlan — `/api/testplan`

| 方法 | 路由 | 权限 | 说明 |
|------|------|------|------|
| GET | `/list` | `perm:api.monitor.view` | 计划列表 |
| GET | `/{id}` | `perm:api.monitor.view` | 计划详情 |
| POST | `/` | `perm:api.monitor.create` | 创建计划 |
| PUT | `/{id}` | `perm:api.monitor.update` | 更新计划 |
| DELETE | `/{id}` | `perm:api.monitor.delete` | 删除计划 |
| POST | `/{id}/run` | `perm:api.monitor.run` | 执行计划，返回 `{ planRunId }` |
| GET | `/{id}/report?planRunId=` | `perm:api.monitor.view` | 执行报告 (JSON) |
| GET | `/{id}/report/html?planRunId=` | `perm:api.monitor.view` | 执行报告 (HTML) |
| GET | `/{id}/runs` | `perm:api.monitor.view` | 历史执行批次 |

### Auth — `/api/auth`

| 方法 | 路由 | 说明 |
|------|------|------|
| POST | `/login` | 登录，返回 accessToken + refreshToken |
| POST | `/refresh` | 刷新 accessToken（轮换 refreshToken） |
| POST | `/logout` | 注销 |
| POST | `/bootstrap` | 首次启动创建管理员 |

### Dashboard — `/api/dashboard`

| 方法 | 路由 | 说明 |
|------|------|------|
| GET | `/?range=24h` | 概览统计（慢请求/失败/最近） |

### Logs — `/api/logs`

| 方法 | 路由 | 说明 |
|------|------|------|
| GET | `/` | 查询日志（按级别/时间/关键字过滤） |
| DELETE | `/` | 清除日志 |

### RBAC — `/api/rbac`

| 方法 | 路由 | 说明 |
|------|------|------|
| GET | `/roles` | 角色列表 |
| GET | `/permissions` | 权限列表 |
| GET | `/roles/{roleId}/permissions` | 角色的权限 |
| PUT | `/roles/{roleId}/permissions` | 设置角色权限 |
| GET | `/users` | 用户列表 |
| GET | `/users/{userId}/role` | 用户的角色 |
| PUT | `/users/{userId}/role` | 设置用户角色 |

### 创建监控 DTO 示例

```json
{
  "Name": "百度健康检查",
  "TargetType": "HTTP",
  "TargetConfig": "{\"Url\":\"https://www.baidu.com\",\"Method\":\"Get\",\"TimeoutSeconds\":10}",
  "IsEnabled": true,
  "AutoDailyEnabled": true,
  "AutoDailyTime": "09:00",
  "Assertions": [
    { "Id": "guid", "Type": "HTTP", "ConfigJson": "{\"Field\":\"StatusCode\",\"Operator\":\"Equal\",\"Expected\":200}" }
  ]
}
```

### 创建测试计划 DTO 示例

```json
{
  "Name": "核心服务体检",
  "Description": "每天早上检查核心 API + 数据库",
  "MonitorIds": ["guid1", "guid2"]
}
```

### 实时推送 — SignalR

| Hub | 路由 | 事件 |
|-----|------|------|
| MonitorHub | `/hubs/monitor` | `monitorUpdated`（监控状态变更） |
| LogHub | `/hubs/log` | 日志流推送 |

---

## DSL 模板语法

### 基本结构

```json
{
  "steps": [
    {
      "name": "步骤名",
      "type": "http",
      "input": { "url": "{{baseUrl}}/api", "method": "Get", "timeout": 10 },
      "extract": [{ "name": "token", "source": "Body", "method": "JsonPath", "expression": "$.token" }],
      "assertions": [{ "field": "StatusCode", "operator": "Equal", "expected": "200" }],
      "retry": { "count": 2, "delayMs": 1000, "backoff": "exponential" },
      "timeout": "30s",
      "onFailure": "stop"
    }
  ],
  "parallel": [
    {
      "name": "并行组",
      "mode": "all",
      "steps": [ /* 步骤定义同上 */ ]
    }
  ],
  "timeout": 60
}
```

### 变量语法

| 语法 | 含义 |
|------|------|
| `{{var}}` | 必须由 TemplateVariablesJson 或 --var 提供 |
| `{{var:default}}` | 未提供时使用默认值 |

变量来源：
- 创建监控时 `TemplateVariablesJson` 字段
- CLI: `--var key=value`
- 运行时：上一步 `extract` 提取的值

### 步骤类型

#### HTTP

```json
{
  "type": "http",
  "input": {
    "url": "...", "method": "Get|Post|Put|Delete",
    "body": { "type": "Json|FormUrlEncoded|Raw", "value": {} },
    "headers": {}, "query": {},
    "timeout": 10
  }
}
```

#### TCP

```json
{
  "type": "tcp",
  "input": {
    "host": "...", "port": 6379,
    "useTls": false, "ignoreSslErrors": false,
    "messages": ["PING"],
    "connectTimeoutMs": 15000
  }
}
```

#### DB

```json
{
  "type": "db",
  "input": {
    "dbType": "sqlserver|mysql|postgresql",
    "connectionString": "...",
    "sql": "SELECT COUNT(*) FROM Orders WHERE Status = 'Pending'",
    "commandType": "Query|NonQuery|Scalar",
    "timeoutSeconds": 30
  }
}
```

#### Python

```json
{
  "type": "python",
  "input": {
    "scriptContent": "import sys; sys.exit(0)",
    "timeoutSeconds": 60,
    "successExitCodes": [0]
  }
}
```

### 断言字段

| 步骤类型 | 可用字段 |
|---------|---------|
| HTTP | `StatusCode`, `Body`, `ResponseTime`, `Elapsed`, `Header` + HeaderKey |
| TCP | `Body`, `Elapsed` |
| DB | `Scalar`, `Body`, `Elapsed` |
| Python | `Body`, `Elapsed` |

运算符：`Equal`, `NotEquals`, `Contains`, `LessThan`, `GreaterThan`

### 提取方法

| 方法 | 说明 | 示例 |
|------|------|------|
| JsonPath | JSON 路径提取 | `$.data.token` |
| Regex | 正则捕获组 | `token:(\w+)` |
| Plain | 原始文本 | 整个响应体 |

提取的变量在后续步骤中用 `{{变量名}}` 引用。冲突检测：如果上一步已有同名变量，需使用 `{{步骤名.变量名}}` 引用。

### 失败策略

| 策略 | 行为 |
|------|------|
| `stop` | 终止整个 DAG |
| `skip` | 跳过当前步骤，继续执行 |
| `ignore` | 标记失败但不影响后续 |

### 并行组模式

| 模式 | 行为 |
|------|------|
| `all` | 全部成功才算通过 |
| `any` | 任一成功即通过 |

### 断路保护

- 键 = `{步骤类型}:{输入JSON}`
- 阈值：5 次失败 / 60 秒窗口
- 触发后直接跳过该步骤
- CLI 环境：作用域为单次执行

---

## 配置参考

### appsettings.json

```json
{
  "Database": {
    "Provider": "SqlServer",
    "ConnectionString": "Server=localhost;Database=AutoTest;..."
  },
  "ConnectionStrings": {
    "HangfireConnection": "..."
  },
  "Redis": { "ConnectionString": "localhost:6379" },
  "AI": {
    "Endpoint": "https://...",
    "ApiKey": "...",
    "Model": "doubao-pro-32k",
    "Worker": {
      "PollIntervalMs": 1000,
      "BatchSize": 10,
      "MaxConcurrency": 4,
      "MaxRetryCount": 3
    }
  },
  "Outbox": {
    "PollIntervalMs": 1000,
    "BatchSize": 20,
    "LockSeconds": 60,
    "Webhook": { "Url": "钉钉机器人地址", "Secret": "签名密钥" }
  },
  "Jwt": {
    "Secret": "至少32字符的密钥",
    "Issuer": "AutoTest",
    "AccessTokenExpirationHours": 8
  }
}
```

### Docker Compose 环境变量

| 变量 | 说明 |
|------|------|
| `AUTOTEST_DB_CONNECTION` | 数据库连接字符串 |
| `AUTOTEST_REDIS_CONNECTION` | Redis 连接字符串 |
| `AUTOTEST_JWT_SIGNING_KEY` | JWT 签名密钥（≥32字符） |
| `AUTOTEST_AI_ENDPOINT` | AI API 地址 |
| `AUTOTEST_AI_APIKEY` | AI API 密钥 |
| `AUTOTEST_OUTBOX_WEBHOOK_URL` | 钉钉机器人 Webhook URL |

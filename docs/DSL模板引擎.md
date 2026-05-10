# DSL 模板引擎

## 概述

TEMPLATE 类型监控使用 JSON 定义的 DAG（有向无环图）描述多步工作流。与普通监控不同的地方：

| 特性 | 普通监控 | 模板监控 |
|------|---------|---------|
| 步骤数 | 1（单一引擎执行） | N（DAG 编排） |
| 变量 | 无 | `{{var}}` / `{{var:default}}` |
| 并行 | 不支持 | ParallelGroup 支持 |
| 断言 | 顶层 MonitorEntity.Assertions | 每步 AssertionDef |
| 重试 | 引擎内重试 | 引擎内重试 + DSL 级 RetryPolicy |
| 断路 | 无 | CircuitBreaker（每步输入键） |
| 进度恢复 | 无 | Redis 断点续跑 |

## DSL JSON 结构

```json
{
  "steps": [
    {
      "name": "loginApi",
      "type": "http",
      "input": {
        "url": "{{baseUrl}}/v1/login",
        "method": "Post",
        "body": { "type": "Json", "value": { "username": "admin", "password": "{{password}}" } },
        "headers": { "Content-Type": "application/json" },
        "timeout": 10
      },
      "extract": [
        { "name": "token", "source": "Body", "method": "JsonPath", "expression": "$.token" }
      ],
      "assertions": [
        { "field": "StatusCode", "operator": "Equal", "expected": "200" }
      ],
      "retry": { "count": 2, "delayMs": 1000, "backoff": "exponential" },
      "timeout": "30s",
      "onFailure": "stop"
    },
    {
      "name": "getUser",
      "type": "http",
      "input": {
        "url": "{{baseUrl}}/v1/user/me",
        "method": "Get",
        "headers": { "Authorization": "Bearer {{token}}" },
        "timeout": 10
      },
      "assertions": [
        { "field": "StatusCode", "operator": "Equal", "expected": "200" },
        { "field": "Body", "operator": "Contains", "expected": "username" }
      ]
    }
  ],
  "parallel": [
    {
      "name": "healthCheck",
      "mode": "all",
      "timeout": "15s",
      "steps": [
        {
          "name": "redisPing", "type": "tcp",
          "input": { "host": "{{redisHost}}", "port": 6379, "messages": ["PING"] },
          "assertions": [{ "field": "Body", "operator": "Contains", "expected": "PONG" }]
        },
        {
          "name": "dbCheck", "type": "db",
          "input": { "dbType": "sqlserver", "connectionString": "{{dbConn}}", "sql": "SELECT 1", "commandType": "Scalar" },
          "assertions": [{ "field": "Scalar", "operator": "Equal", "expected": "1" }]
        }
      ]
    }
  ],
  "timeout": 60
}
```

## 变量语法

| 语法 | 含义 | 示例 |
|------|------|------|
| `{{var}}` | 必须由 TemplateVariablesJson 提供 | `{{baseUrl}}` |
| `{{var:default}}` | 未提供时使用默认值 | `{{host:localhost}}` |

变量值来自 Monitor.TemplateVariablesJson：
```json
{"baseUrl": "https://api.example.com", "password": "secret123"}
```

## 步骤类型

### HTTP

```json
{
  "type": "http",
  "input": {
    "url": "...", "method": "Get|Post|Put|Delete",
    "body": { "type": "Json|FormUrlEncoded|Raw", "value": {...} },
    "headers": {}, "query": {},
    "timeout": 10
  }
}
```

### TCP

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

### DB

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

### Python

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

## 断言字段参考

| 步骤类型 | 可用字段 |
|---------|---------|
| HTTP | `StatusCode`, `Body`, `ResponseTime`, `Elapsed`, `Header:{key}` |
| TCP | `Connected`, `Body`, `LatencyMs`, `SequenceCorrect` |
| DB | `RowValue:{row}.{col}`, `AffectedRows`, `Scalar`, `ElapsedMilliseconds` |
| Python | `ExitCode`, `StdOut`, `StdErr`, `ElapsedMs`, `TimedOut` |

运算符：`Equal`, `NotEqual`, `Contains`, `LessThan`, `GreaterThan`

## 提取表达式

| 方法 | 说明 | 示例 |
|------|------|------|
| JsonPath | JSON 路径提取 | `$.data.token` |
| Regex | 正则捕获组 | `token:(\w+)` |
| Plain | 原始文本 | `整个响应体` |

提取的变量可在后续步骤中通过 `{{变量名}}` 引用。

## 执行引擎行为

### 失败策略
- `stop`：终止整个 DAG
- `skip`：跳过当前步骤，继续执行
- `ignore`：标记当前步骤失败但不影响后续

### 并行组
- `mode: "all"`：全部成功才算通过，任一失败终止
- `mode: "any"`：任一成功即通过

### 断路保护
- 键 = `{步骤类型}:{输入JSON}`
- 阈值：5 次失败 / 60 秒窗口
- 触发后直接跳过该步骤

### 进度恢复
- 每步执行后序列化 DslRuntimeContext → Redis
- 崩溃重启后从 `CurrentStepIndex` 继续
- TTL 6 小时

## 示例文件

| 文件 | 说明 |
|------|------|
| `01-http-baidu-health.json` | HTTP 单步健康检查 |
| `02-http-login-api.json` | HTTP 带变量登录接口 |
| `03-tcp-redis-ping.json` | TCP Redis PING |
| `04-tcp-mysql-port.json` | TCP MySQL 端口探测 |
| `05-tcp-tls-cert.json` | TCP TLS 证书检查 |
| `06-db-connection.json` | DB SELECT 1 |
| `07-db-order-consistency.json` | DB 超时订单告警 |
| `08-multi-service-health.json` | 并行三服务体检 |
| `09-e2e-login-flow.json` | 全链路：登录→查用户→校验 |

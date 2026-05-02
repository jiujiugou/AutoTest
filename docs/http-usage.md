# HTTP 监控 — 用户使用指南

---

## 三分钟快速开始

### 第一步：创建一个 HTTP 监控任务

在系统中添加一个监控任务（通过 API 或管理界面），检查百度是否可达：

```json
{
  "name": "百度首页可达性检查",
  "targetType": "HTTP",
  "targetConfig": {
    "url": "https://www.baidu.com",
    "method": "Get",
    "timeout": 10
  },
  "assertions": [
    {
      "type": "Http",
      "configJson": "{ \"field\": \"StatusCode\", \"operator\": \"Equal\", \"expected\": \"200\" }"
    }
  ]
}
```

**你需要改的地方**：`url` 换成你要检查的地址。

### 第二步：系统自动执行

创建成功后，系统会按调度配置自动执行（默认每日执行，也可手动触发一次）。

### 第三步：查看结果

- **成功**：状态码返回 200，断言通过，任务标记为成功
- **失败**：状态码不对或请求超时，系统记录失败详情，触发 AI 根因分析

---

## 常见使用场景

### 场景 1：API 健康检查

定期检查你的后端服务是否活着。

```json
{
  "name": "用户服务健康检查",
  "targetType": "HTTP",
  "targetConfig": {
    "url": "https://your-service.com/api/health",
    "method": "Get",
    "timeout": 5,
    "headers": {
      "Accept": ["application/json"]
    }
  },
  "assertions": [
    {
      "type": "Http",
      "configJson": "{ \"field\": \"StatusCode\", \"operator\": \"Equal\", \"expected\": \"200\" }"
    },
    {
      "type": "Http",
      "configJson": "{ \"field\": \"ResponseTime\", \"operator\": \"LessThan\", \"expected\": \"3000\" }"
    }
  ]
}
```

| 你需要改的字段 | 改为你的值 |
|---------------|-----------|
| `url` | 你的健康检查地址 |
| `timeout` | 建议 5~10 秒，超时说明服务可能出问题 |
| `expected` (StatusCode) | 你的 API 正常返回的状态码 |

**检查结果说明**：状态码匹配 + 响应时间 < 3 秒 → 健康；否则触发告警。

---

### 场景 2：需要登录 Token 的接口

调用需要 Bearer Token 授权的接口。

```json
{
  "name": "获取用户信息",
  "targetType": "HTTP",
  "targetConfig": {
    "url": "https://your-service.com/api/user/profile",
    "method": "Get",
    "authType": "Bearer",
    "authToken": "eyJhbGciOiJIUzI1NiIs..."
  },
  "assertions": [
    {
      "type": "Http",
      "configJson": "{ \"field\": \"StatusCode\", \"operator\": \"Equal\", \"expected\": \"200\" }"
    }
  ]
}
```

| 你需要改的字段 | 说明 |
|---------------|------|
| `url` | 你的 API 地址 |
| `authToken` | 换成你的 JWT Token |

> **什么时候用**：你的接口需要 `Authorization: Bearer <token>` 才能访问。

其他认证方式：

| 认证方式 | 需要填的字段 | 适用场景 |
|---------|-------------|---------|
| `Bearer` | `authToken` | JWT / OAuth2 Token |
| `Basic` | `authUsername` + `authPassword` | 基本认证接口 |
| `ApiKeyHeader` | `authToken` | 在 `X-Api-Key` 头中传密钥 |

---

### 场景 3：需要登录 Session（Cookie）的接口

先登录拿到 Cookie，再用这个 Cookie 访问后续页面。

```json
{
  "name": "需要登录的页面检查",
  "targetType": "HTTP",
  "targetConfig": {
    "url": "https://your-service.com/dashboard",
    "method": "Get",
    "useCookies": true,
    "authType": "Bearer",
    "authToken": "登录接口返回的token"
  },
  "assertions": [
    {
      "type": "Http",
      "configJson": "{ \"field\": \"StatusCode\", \"operator\": \"Equal\", \"expected\": \"200\" }"
    }
  ]
}
```

> **什么时候用**：你的服务端使用 Session 机制，登录后服务端通过 Set-Cookie 下发 SessionId，客户端后续请求需要自动携带这个 Cookie。

**注意**：如果 `useCookies: true` 但接口仍然提示未登录，检查你的登录流程是否返回了正确的 Set-Cookie 响应头。

---

### 场景 4：提交 JSON 数据

```json
{
  "name": "创建订单",
  "targetType": "HTTP",
  "targetConfig": {
    "url": "https://your-service.com/api/orders",
    "method": "Post",
    "body": {
      "type": "Json",
      "contentType": "application/json",
      "value": {
        "productId": "12345",
        "quantity": 1,
        "userId": "67890"
      }
    },
    "timeout": 15,
    "enableRetry": true,
    "retryCount": 2,
    "retryDelayMs": 1000
  },
  "assertions": [
    {
      "type": "Http",
      "configJson": "{ \"field\": \"StatusCode\", \"operator\": \"Equal\", \"expected\": \"201\" }"
    }
  ]
}
```

| 你需要改的字段 | 说明 |
|---------------|------|
| `url` | 你的提交地址 |
| `body.value` | 替换成你要提交的数据 |
| `expected` | 创建成功通常返回 201，如果你的接口返回 200 就改 200 |

> **什么时候用重试**：网络不稳定时建议启重试（比如对接第三方 API），避免偶发网络抖动导致误告警。

---

### 场景 5：需要响应体内容校验

不仅检查状态码，还要检查返回的数据是否正确。

```json
{
  "name": "检查接口返回结果",
  "targetType": "HTTP",
  "targetConfig": {
    "url": "https://your-service.com/api/status",
    "method": "Get"
  },
  "assertions": [
    {
      "type": "Http",
      "configJson": "{ \"field\": \"StatusCode\", \"operator\": \"Equal\", \"expected\": \"200\" }"
    },
    {
      "type": "Http",
      "configJson": "{ \"field\": \"Body\", \"operator\": \"Contains\", \"expected\": \"\\\"status\\\":\\\"ok\\\"\" }"
    }
  ]
}
```

| 断言字段 | 什么时候用 | 操作符举例 |
|---------|-----------|-----------|
| `StatusCode` | 几乎必选，验证 HTTP 状态码 | `Equal` / `NotEqual` |
| `Body` | 需要检查响应正文内容 | `Contains` / `Equal` / `NotContains` |
| `ResponseTime` | 关注接口性能（超时告警） | `LessThan`（< 3 秒） |
| `Header` | 检查响应头（如 Content-Type） | `Contains` / `Equal` |
| `Elapsed` | 和 ResponseTime 类似，整次执行耗时 | `LessThan` / `GreaterThan` |

---

### 场景 6：通过代理访问外部接口

你的服务器在公司内网，需要通过代理才能访问外网。

```json
{
  "name": "外部 API 检查",
  "targetType": "HTTP",
  "targetConfig": {
    "url": "https://external-api.com/status",
    "method": "Get",
    "proxyUrl": "http://proxy.your-company.com:8080",
    "proxyUser": "your-username",
    "proxyPass": "your-password",
    "timeout": 30
  },
  "assertions": [
    {
      "type": "Http",
      "configJson": "{ \"field\": \"StatusCode\", \"operator\": \"Equal\", \"expected\": \"200\" }"
    }
  ]
}
```

> **什么时候用**：你的服务器不能直接访问目标地址，需要走代理。
>
> **匿名代理**：如果代理不需要用户名密码，`proxyUser` 和 `proxyPass` 可以省略。

---

### 场景 7：高并发场景限量（避免打爆服务器）

如果你有大量监控任务同时调用同一个接口，建议开启并发限制。

```json
{
  "name": "高频率接口检查",
  "targetType": "HTTP",
  "targetConfig": {
    "url": "https://your-service.com/api/status",
    "method": "Get",
    "enableRateLimit": true,
    "maxConcurrency": 10
  },
  "assertions": [
    {
      "type": "Http",
      "configJson": "{ \"field\": \"StatusCode\", \"operator\": \"Equal\", \"expected\": \"200\" }"
    }
  ]
}
```

| 字段 | 建议值 | 说明 |
|------|-------|------|
| `maxConcurrency` | 5~20 | 同一时间最多允许多少个请求并发执行 |

> **什么时候用**：你有多个监控任务同时调用同一个 API，且该 API 有 QPS 限制时。

---

## 如何判断监控是否成功

一次 HTTP 监控的执行结果包含三个层次：

```
执行结果
├── 执行过程 (IsExecutionSuccess)
│   ├── true  → 请求成功发出且收到响应（不代表业务正确）
│   └── false → 网络不通 / 超时 / DNS 解析失败
│
├── 断言结果 (AssertionResult)
│   ├── StatusCode == 200 ？  ← 最常用
│   ├── Body 包含预期内容？    ← 业务校验
│   └── ResponseTime < 3秒？  ← 性能校验
│
└── AI 分析结果（仅失败时触发）
    └── 自动分析根因、给出修复建议
```

**看执行记录列表**：
- `成功` = 执行过程成功 + 所有断言通过
- `失败` = 执行过程失败，或至少一个断言不通过

---

## 常见问题（FAQ）

### Q1：总是超时怎么办？

检查以下几点：

1. **目标地址是否能从你的服务器访问？** — 在服务器上手动 `curl <你的地址>` 试试
2. **需要代理吗？** — 公司内网访问外网通常需要配 `proxyUrl`
3. **超时时间设得太短？** — `timeout` 建议至少 10 秒，慢接口可以设到 30 秒
4. **启用了重试吗？** — 偶发超时可能是网络抖动，开重试可以避免误告警

### Q2：返回了 401 / 403 未授权

1. 检查 `authType` 是否设置正确（Bearer / Basic / ApiKeyHeader）
2. 检查 `authToken` 或 `authUsername`/`authPassword` 是否填写正确
3. Token 是否已过期？重新获取后更新

### Q3：Cookie 不生效，还是提示未登录

1. 确认 `useCookies` 设为 `true`
2. 检查登录接口的响应头是否有 `Set-Cookie`
3. 如果 Set-Cookie 中标记了 `HttpOnly`、`Secure`、`Domain`，请确认浏览器/CookieContainer 是否满足这些条件
4. Cookie 可能有有效期，过期后需要重新登录

### Q4：返回的状态码是 0，是什么意思？

状态码 0 表示请求**没有收到任何响应**，可能原因：

- 网络不通（DNS 解析失败、连接被拒绝）
- 请求超时（超过 `timeout` 时间）
- 证书错误（如果用了自签名证书，请设 `ignoreSslErrors: true`）

### Q5：重试了还是不成功？

重试只重试**网络级别异常**（超时、连接失败）。如果接口返回 500，重试也会继续返回 500 —— 此时是服务端的问题，重试解决不了。

### Q6：如何手动触发一次执行？

目前通过 API 手动触发执行的功能需要调用执行引擎。后续版本会提供"立即执行"按钮。

---

## 完整配置参考（附录）

以下是 `targetConfig` 支持的所有字段，按需查阅。

```json
{
  "url": "https://api.example.com/users",
  "method": "Get",
  
  "body": {
    "type": "Json",
    "contentType": "application/json",
    "value": { "key": "value" }
  },

  "headers": {
    "Accept": ["application/json"],
    "X-Custom-Header": ["value1"]
  },

  "query": { "page": "1", "size": "20" },

  "timeout": 30,

  "authType": "Bearer",
  "authToken": "...",
  "authUsername": null,
  "authPassword": null,

  "useCookies": false,
  "allowAutoRedirect": true,
  "maxRedirects": 5,
  "ignoreSslErrors": false,

  "proxyUrl": null,
  "proxyUser": null,
  "proxyPass": null,

  "enableRetry": false,
  "retryCount": 3,
  "retryDelayMs": 1000,

  "enableRateLimit": false,
  "maxConcurrency": 5
}
```

### 字段速查表

| 字段 | 默认值 | 什么时候需要改 |
|------|--------|--------------|
| `url` | 必填 | 每次都要填 |
| `method` | 必填 | 你的接口请求方法 |
| `body` | null | 发送数据时（POST / PUT / PATCH） |
| `headers` | null | 需要自定义请求头时 |
| `query` | null | URL 参数较多，不想拼在 url 里时 |
| `timeout` | 30 秒 | 接口响应慢时调大，要快速失败时调小 |
| `authType` | null | 接口需要认证时 |
| `authToken` | null | 使用 Bearer 或 ApiKeyHeader 时 |
| `authUsername` | null | 使用 Basic 认证时 |
| `authPassword` | null | 使用 Basic 认证时 |
| `useCookies` | false | 接口依赖 Session / Cookie 时 |
| `allowAutoRedirect` | true | 不需要自动跟随重定向时设为 false |
| `ignoreSslErrors` | false | 测试环境自签名证书时设为 true |
| `proxyUrl` | null | 需要走代理访问时 |
| `enableRetry` | false | 网络不稳定时 |
| `retryCount` | 3 | 重试次数，越大越容忍网络抖动 |
| `retryDelayMs` | 1000 | 每次重试间隔（毫秒） |
| `enableRateLimit` | false | 并发请求量大时 |
| `maxConcurrency` | 5 | 限制同时发送的请求数 |

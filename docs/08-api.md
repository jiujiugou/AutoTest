# 对外接口

## 8.1 REST API

### 监控任务管理

| 方法 | 路径 | 说明 |
|------|------|------|
| `GET` | `/api/monitor` | 监控任务列表 |
| `GET` | `/api/monitor/{id}` | 监控详情 |
| `POST` | `/api/monitor` | 创建监控任务 |
| `PUT` | `/api/monitor/{id}` | 更新监控任务 |
| `DELETE` | `/api/monitor/{id}` | 删除监控任务 |
| `PUT` | `/api/monitor/{id}/enable` | 启用/禁用 |
| `PUT` | `/api/monitor/{id}/schedule` | 设置调度参数 |

### 执行记录

| 方法 | 路径 | 说明 |
|------|------|------|
| `GET` | `/api/monitor/{id}/executions` | 执行记录列表 |
| `GET` | `/api/monitor/{id}/executions/latest` | 最新一次执行 |
| `GET` | `/api/monitor/{id}/executions/{executionId}/assertions` | 断言结果 |
| `GET` | `/api/monitor/{id}/stats` | 运行统计 + Top 错误 |

### 看板

| 方法 | 路径 | 说明 |
|------|------|------|
| `GET` | `/api/dashboard/summary` | 全局概览（总数/成功率） |
| `GET` | `/api/dashboard/trend` | 成功率趋势 |
| `GET` | `/api/dashboard/errors` | Top 错误分布 |

### 日志

| 方法 | 路径 | 说明 |
|------|------|------|
| `GET` | `/api/logs` | 日志查询（ES） |
| `POST` | `/api/logs/batch` | 批量写入日志 |

### 认证

| 方法 | 路径 | 说明 |
|------|------|------|
| `POST` | `/api/auth/login` | 登录获取 JWT |
| `GET` | `/api/auth/me` | 当前用户信息 |
| `POST` | `/api/auth/refresh` | 刷新 Token |

### RBAC

| 方法 | 路径 | 说明 |
|------|------|------|
| `GET` | `/api/rbac/users` | 用户列表 |
| `POST` | `/api/rbac/users` | 创建用户 |
| `PUT` | `/api/rbac/users/{id}/roles` | 分配角色 |
| `GET` | `/api/rbac/roles` | 角色列表 |
| `POST` | `/api/rbac/roles` | 创建角色 |
| `GET` | `/api/rbac/permissions` | 权限列表 |

### AI 分析结果（扩展）

| 方法 | 路径 | 说明 |
|------|------|------|
| `GET` | `/api/analysis/{outboxMessageId}` | 按 OutboxMessageId 查询分析结果 |
| `GET` | `/api/analysis/list` | 分析结果列表 |
| `GET` | `/api/analysis/{id}/recovery` | 查看自动恢复记录 |

## 8.2 SignalR Hubs

### MonitorHub

- 路径：`/hubs/monitor`
- 认证：JWT (Query String)
- 事件：
  - `ExecutionCompleted` — 执行完成通知
  - `MonitorUpdated` — 监控任务变更通知

### LogHub

- 路径：`/hubs/logs`
- 认证：JWT (Query String)
- 事件：
  - `LogReceived` — 实时日志推送

### 前端订阅示例

```javascript
const connection = new signalR.HubConnectionBuilder()
    .withUrl("/hubs/monitor?access_token=" + token)
    .build();

connection.on("ExecutionCompleted", (data) => {
    console.log("执行完成:", data);
});
```

## 8.3 错误码

| HTTP Status | 说明 |
|-------------|------|
| 200 | 成功 |
| 400 | 参数错误（FluentValidation 校验失败） |
| 401 | 未认证（JWT 缺失或过期） |
| 403 | 无权限（RBAC 拦截） |
| 404 | 资源不存在 |
| 409 | 幂等冲突（重复执行） |
| 500 | 服务端错误 |

## 8.4 认证方式

所有受保护的 API 需要携带 JWT Token：

```
Authorization: Bearer {token}
```

SignalR 连接通过 Query String 传递 Token：

```
/hubs/monitor?access_token={token}
```

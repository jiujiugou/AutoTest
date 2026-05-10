# API 参考

所有接口需要 JWT Bearer Token。权限格式：`perm:resource.action`。

## Monitor — `/api/monitor`

| 方法 | 路由 | 权限 | 说明 |
|------|------|------|------|
| GET | `/` | `api.monitor.read` | 监控列表（50条） |
| GET | `/{id}` | `api.monitor.view` | 单个详情（含断言） |
| POST | `/` | `api.monitor.write` | 创建 |
| PUT | `/{id}` | `api.monitor.write` | 更新 |
| DELETE | `/{id}` | `api.monitor.delete` | 删除 |
| PATCH | `/{id}/enabled` | `api.monitor.write` | 启用/禁用 |
| POST | `/{id}/run` | `api.monitor.execute` | 立即执行（Hangfire 入队） |
| GET | `/{id}/executions` | `api.monitor.view` | 执行记录列表 |
| GET | `/{id}/executions/latest` | `api.monitor.view` | 最新执行记录 |
| GET | `/{id}/executions/{execId}/assertions` | `api.monitor.view` | 断言结果 |
| GET | `/{id}/executions/{execId}/analysis` | `api.analysis.read` | AI 分析结果 |
| GET | `/{id}/analysis` | `api.analysis.read` | 所有 AI 分析 |
| GET | `/{id}/runtime-stats` | `api.monitor.view` | 运行统计 + TopN 错误 |

## Auth — `/api/auth`

| 方法 | 路由 | 说明 |
|------|------|------|
| POST | `/login` | 登录，返回 accessToken + refreshToken |
| POST | `/refresh` | 刷新 accessToken（轮换 refreshToken） |
| POST | `/logout` | 注销，撤销所有 refreshToken |
| POST | `/bootstrap` | 首次启动创建管理员 |

## Dashboard — `/api/dashboard`

| 方法 | 路由 | 权限 | 说明 |
|------|------|------|------|
| GET | `/` | `api.dashboard.read` | 概览统计（慢请求/失败/最近） |

## Logs — `/api/logs`

| 方法 | 路由 | 权限 | 说明 |
|------|------|------|------|
| GET | `/` | `api.log.read` | 查询日志（按级别/时间/关键字/服务过滤） |
| DELETE | `/` | `api.log.delete` | 清除日志 |

## RBAC — `/api/rbac`

| 方法 | 路由 | 权限 | 说明 |
|------|------|------|------|
| GET | `/roles` | `api.rbac.read` | 角色列表 |
| GET | `/permissions` | `api.rbac.read` | 权限列表 |
| GET | `/roles/{roleId}/permissions` | `api.rbac.read` | 角色的权限 |
| PUT | `/roles/{roleId}/permissions` | `api.rbac.write` | 设置角色权限 |
| GET | `/users` | `api.user.read` | 用户列表 |
| GET | `/users/{userId}/role` | `api.user.read` | 用户的角色 |
| PUT | `/users/{userId}/role` | `api.user.write` | 设置用户角色 |

## 实时推送 — SignalR

| Hub | 路由 | 事件 | 说明 |
|-----|------|------|------|
| MonitorHub | `/hubs/monitor` | `monitorUpdated` | 监控状态变更 |
| LogHub | `/hubs/log` | 日志流 | Serilog Sink → 客户端 |

---

## DTO 示例

### 创建监控 (POST /api/monitor)

```json
{
  "Name": "百度健康检查",
  "TargetType": "TEMPLATE",
  "TargetConfig": "{\"steps\":[{\"name\":\"check\",\"type\":\"http\",\"input\":{\"url\":\"https://www.baidu.com\",\"method\":\"Get\",\"timeout\":10}}]}",
  "IsEnabled": true,
  "IsTemplate": true,
  "TemplateVariablesJson": null,
  "AutoDailyEnabled": false,
  "AutoDailyTime": "09:00",
  "MaxRuns": null,
  "Assertions": []
}
```

### 执行记录响应

```json
{
  "Id": "guid",
  "MonitorId": "guid",
  "Status": 3,
  "StartedAt": "2026-05-09T05:44:56Z",
  "FinishedAt": "2026-05-09T05:44:56Z",
  "IsExecutionSuccess": false,
  "ErrorMessage": "No execution engine found for target TEMPLATE",
  "ResultType": "Exception",
  "Assertions": []
}
```

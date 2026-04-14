# AutoTestWeb（前端）

## 启动

1) 启动后端 AutoTest.Webapi（默认代理到 `http://localhost:5033`）

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

## 已对接的后端接口

- 认证：`/api/auth/login`、`/api/auth/refresh`（401 自动 refresh）、`/api/auth/bootstrap`、`/api/auth/logout`
- 监控：`/api/monitor/*`
- RBAC：`/api/rbac/*`
- AI：`/api/ai/chat`

# 权限分层命名空间模型

> 版本: v2.0 | 状态: Draft

---

## 1. 设计原则

### 1.1 三层命名空间

```
api.*     — 后端接口权限（API 访问控制）
ui.*      — 前端展示权限（菜单/按钮可见性）
data.*    — 数据访问权限（扩展预留）
```

### 1.2 核心规则

| 规则 | 说明 |
|------|------|
| **权限与 UI/API 解耦** | `api.*` 只控制后端接口，`ui.*` 只控制前端展示，互不依赖 |
| **权限只绑定角色** | 不能给单个用户设置专属权限，所有权限通过角色关联 |
| **管理员管所有** | Admin 可以：创建用户、重置密码、分配角色、配置角色权限 |
| **用户不能改权限** | 用户无权限管理入口，只能看到自己被允许的菜单和操作 |
| **ui.* 控制菜单** | 前端根据当前用户权限列表中的 `ui.*` 权限，动态显示/隐藏菜单项 |
| **api.* 控制 API** | 后端 `[Authorize(Policy = "perm:api.monitor.run")]` 保护接口 |

---

## 2. 权限定义

### 2.1 完整权限列表

```json
[
  { "code": "api.dashboard.view",  "name": "查看仪表盘" },
  { "code": "api.logs.view",       "name": "查看系统日志" },

  { "code": "api.monitor.view",    "name": "查看监控" },
  { "code": "api.monitor.run",     "name": "运行监控" },
  { "code": "api.monitor.create",  "name": "创建监控" },
  { "code": "api.monitor.update",  "name": "更新监控" },
  { "code": "api.monitor.delete",  "name": "删除监控" },

  { "code": "api.tasks.view",      "name": "查看任务" },
  { "code": "api.tasks.manage",    "name": "任务管理" },

  { "code": "api.settings.view",   "name": "查看设置" },
  { "code": "api.settings.manage", "name": "管理设置" },

  { "code": "ui.menu.ai",          "name": "AI 助手菜单" },
  { "code": "ui.menu.rbac",        "name": "权限管理菜单" },
  { "code": "ui.menu.person",      "name": "个人中心菜单" }
]
```

### 2.2 命名空间说明

| 命名空间 | 用途 | 示例 |
|----------|------|------|
| `api.*` | 后端 API 访问控制 | `api.monitor.create` 控制创建监控的 API |
| `ui.*` | 前端 UI 元素可见性 | `ui.menu.rbac` 控制权限管理菜单是否显示 |
| `data.*` | 数据级权限（预留） | `data.monitor.all`（预留） |

### 2.3 默认角色权限分配

| 权限 | admin | user |
|------|-------|------|
| `api.dashboard.view` | ✅ | ✅ |
| `api.logs.view` | ✅ | ✅ |
| `api.monitor.view` | ✅ | ✅ |
| `api.monitor.run` | ✅ | ❌ |
| `api.monitor.create` | ✅ | ❌ |
| `api.monitor.update` | ✅ | ❌ |
| `api.monitor.delete` | ✅ | ❌ |
| `api.tasks.view` | ✅ | ✅ |
| `api.tasks.manage` | ✅ | ❌ |
| `api.settings.view` | ✅ | ❌ |
| `api.settings.manage` | ✅ | ❌ |
| `ui.menu.ai` | ✅ | ❌ |
| `ui.menu.rbac` | ✅ | ❌ |
| `ui.menu.person` | ✅ | ✅ |

---

## 3. 后端实现

### 3.1 权限策略名格式

```
perm:[namespace].[resource].[action]
```

示例：
- `perm:api.monitor.run`
- `perm:api.settings.manage`
- `perm:ui.menu.rbac`（前端使用，后端不校验）

### 3.2 Controller 使用示例

```csharp
[HttpPost]
[Authorize(Policy = "perm:api.monitor.create")]
public async Task<IActionResult> Add([FromBody] MonitorDto dto) { ... }

[HttpDelete("{id}")]
[Authorize(Policy = "perm:api.monitor.delete")]
public async Task<IActionResult> Delete(Guid id) { ... }
```

### 3.3 前端权限判断

```javascript
// Layout.vue — 菜单权限
function hasPerm(code) {
  return permissions.value.includes(code)
}
```

```html
<el-menu-item v-if="hasPerm('ui.menu.rbac')" index="/RbacAdmin">
  <el-icon><User /></el-icon>
  <template #title>权限管理</template>
</el-menu-item>
```

### 3.4 登录返回数据结构

```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIs...",
  "refreshToken": "abc123...",
  "user": {
    "id": 1,
    "username": "admin",
    "role": "admin",
    "permissions": [
      "api.dashboard.view",
      "api.monitor.view",
      "api.monitor.run",
      "ui.menu.person",
      "ui.menu.rbac"
    ]
  }
}
```

---

## 4. 现有权限迁移对照

| 旧权限名 | 新权限名 | 命名空间 |
|----------|---------|---------|
| `dashboard.view` | `api.dashboard.view` | API |
| `logs.view` | `api.logs.view` | API |
| `monitor.view` | `api.monitor.view` | API |
| `monitor.run` | `api.monitor.run` | API |
| `monitor.create` | `api.monitor.create` | API |
| `monitor.update` | `api.monitor.update` | API |
| `monitor.delete` | `api.monitor.delete` | API |
| `tasks.view` | `api.tasks.view` | API |
| `tasks.manage` | `api.tasks.manage` | API |
| `settings.view` | `api.settings.view` | API |
| `settings.manage` | `api.settings.manage` | API |
| `menu.ai` | `ui.menu.ai` | UI |
| `menu.rbac` | `ui.menu.rbac` | UI |
| `menu.person` | `ui.menu.person` | UI |

---

## 5. 数据库存储

### Permissions 表结构（已有，无需修改）

| 列名 | 类型 | 说明 |
|------|------|------|
| `Id` | int | 主键 |
| `Code` | nvarchar(100) | 权限标识，如 `api.monitor.create` |
| `Name` | nvarchar(200) | 显示名称 |

### Code 索引（已有）

`IX_Permissions_Code` — 唯一索引，权限标识不允许重复。

---

## 6. 权限与 UI 解耦说明

### 为什么 `ui.menu.rbac` 不需要对应的 `api.*` 权限？

```
ui.menu.rbac → 控制前端菜单是否显示
                 ↓
         用户看不到菜单，但知道 URL 仍可直接访问
                 ↓
         RbacController 使用 api.settings.manage 保护
                 ↓
         即使绕过 UI 访问 API，也会被后端拦截
```

**规则**：前端菜单显示用 `ui.*`，后端 API 保护用 `api.*`，两者独立互不依赖。

---

## 7. 扩展：data.* 命名空间（预留）

`data.*` 用于数据级权限控制，当前未实现，预留供未来扩展：

```
data.monitor.all      — 查看所有监控数据
data.monitor.own      — 仅查看自己创建的监控
data.report.export    — 导出报表
```

---

## 8. 改动文件清单

| 文件 | 改动 |
|------|------|
| `SeedRbacDefaults.cs` | 权限 Code 加 `api.` / `ui.` 前缀；新增 3 个菜单权限 |
| `MonitorController.cs` | `perm:monitor.xxx` → `perm:api.monitor.xxx` |
| `LogsController.cs` | `perm:logs.view` → `perm:api.logs.view` |
| `RbacController.cs` | `perm:settings.xxx` → `perm:api.settings.xxx` |
| `PermissionsOptions.cs` | 注释说明更新 |
| `Layout.vue` | 菜单 `v-if` 使用 `ui.menu.*` |
| `rbac-refactoring.md` | 本文档 |

---

## 9. FAQ

### Q1：为什么不用扁平命名，要用分层命名空间？

避免权限膨胀后命名冲突。例如 `menu.ai` 和 `api.ai.run` 在扁平命名下无法区分。分层后 `ui.menu.ai` 控制前端菜单，`api.ai.run` 控制后端 AI 接口。

### Q2：`data.*` 什么时候用？

当需要控制用户只能查看特定范围的数据时（如只看到自己创建的监控），使用 `data.*` 命名空间。当前未实现，仅预留。

### Q3：ui 权限不校验后端，安全吗？

安全。`ui.*` 仅控制前端展示，后端所有 API 都由 `api.*` 权限保护。用户即使绕过前端直接调用 API，也会被后端拦截。

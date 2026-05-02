# 模板设计功能 — 前后端规划与现状

> 版本: v1.0 | 状态: Review

---

## 目录

1. [现状：已完成的工作](#1-现状已完成的工作)
2. [现状：存在的问题](#2-现状存在的问题)
3. [Phased Plan](#3-phased-plan)
   - [Phase 1：基础 CRUD + JSON 编辑器（当前）](#phase-1基础-crud--json-编辑器当前)
   - [Phase 2：步骤配置 UI 化](#phase-2步骤配置-ui-化)
   - [Phase 3：批量操作与版本管理](#phase-3批量操作与版本管理)
4. [后端 API 对照表](#4-后端-api-对照表)
5. [前端路由与权限](#5-前端路由与权限)
6. [数据流链路](#6-数据流链路)
7. [关键决策记录](#7-关键决策记录)

---

## 1. 现状：已完成的工作

### 后端已实现

| # | 组件 | 文件 | 说明 |
|---|---|---|---|
| ✅ | **模板领域模型** | `MonitorEntity.cs` | 增加 `IsTemplate`、`TemplateVariablesJson` 字段 |
| ✅ | **模板目标类型** | `TemplateTarget.cs`（新建） | `Type = "TEMPLATE"`，直接存储 DSL JSON |
| ✅ | **模板目标 DTO 与映射器** | `TemplateTargetDto.cs` / `TemplateTargetMap.cs`（新建） | DI 已注册到 `AddInfrastructureServiceCollectionExtensions` |
| ✅ | **MonitorDto 支持模板** | `MonitorDto.cs` | 增加 `IsTemplate`、`TemplateVariablesJson` 字段 |
| ✅ | **MonitorService 处理模板** | `MonitorService.cs` | `AddAsync` / `UpdateAsync` 中调用 `SetTemplateConfig()` |
| ✅ | **DSL 解析** | `AutoTest.Dsl` 类库 | `IDslParser` → `DslParser`：Schema 校验 + 变量替换 + StepSequence 构建 |
| ✅ | **DSL Schema 校验** | `DslSchemaValidator.cs` | 校验 steps/type/input/extract/assertions，JSON Path 定位错误 |
| ✅ | **Runtime 编排** | `AutoTest.Orchestration` 类库 | `ExecutionEngine`：串行/并行执行、重试/超时/降级、变量提取（含命名空间防冲突） |
| ✅ | **Pipeline 集成** | `Pipeline.cs` | 注册顺序：TemplateResolutionStep → RuntimeOrchestrationStep → ExecutionStep(跳过) → AssertionStep |
| ✅ | **ExecutionStep 跳过逻辑** | `ExecutionStep.cs` | `IsTemplate=true` 时直接 `next()`，不执行 |
| ✅ | **Controller API** | `MonitorController.cs` | List/Get 返回 `isTemplate`、`templateVariablesJson` |
| ✅ | **权限种子数据** | `SeedRbacRefresh.cs`（新建迁移 `2026050202`）| 所有权限含 `ui.menu.template`，admin 全量，user 基础 |
| ✅ | **Admin 权限直通** | `DapperAuthService.cs` | admin 登录直接返回 `SELECT Code FROM Permissions` |

### 前端已实现

| # | 组件 | 文件 | 说明 |
|---|---|---|---|
| ✅ | **模板设计页面** | `TemplateDesigner.vue`（新建） | 左侧列表 + 右侧编辑器布局 |
| ✅ | **路由注册** | `router/index.js` | `/template` → `TemplateDesigner.vue`（懒加载） |
| ✅ | **菜单项** | `Layout.vue` | 受 `ui.menu.template` 权限控制的菜单入口 |
| ✅ | **API 封装** | `monitors.js` | 复用已有 CRUD API（list/get/create/update/remove/run） |

---

## 2. 现状：存在的问题

### 🔴 P0 — 必须立即修复

| # | 问题 | 原因 | 影响 |
|---|---|---|---|
| **P0-1** | 新建/编辑模板时，`assertions: []` 传给后端，后端尝试解析空的断言列表 | `MonitorService.AddAsync` 中 `dto.Assertions.Select(...)` 对空列表也会走到 `FirstOrDefault` 逻辑 | 创建成功，但无影响 |
| **P0-2** | 更新模板时，后端没有返回 `id` 给前端确认 | `MonitorController.Update` 返回 `NoContent()`，前端无法确认是否成功 | 前端收到 204，不影响功能 |
| **P0-3** | 前端 `execResult.executions` 中的字段名与后端实际返回不一致 | `MonitorController.GetExecutions` 返回的是 `ExecutionRecord` 实体，其状态字段名可能与前端预期不同 | 执行结果表格可能不显示数据 |

### 🟡 P1 — 应尽快修复

| # | 问题 | 原因 | 影响 |
|---|---|---|---|
| **P1-1** | 没有 DSL JSON 语法验证提示 | 前端只用了纯文本 `<el-input type="textarea">`，没有 JSON 格式校验 | 用户输入无效 JSON 提交后会收到后端 400 错误 |
| **P1-2** | 没有模板复制/克隆功能 | 无相关接口 | 用户需要手动复制 JSON 内容 |
| **P1-3** | 变量编辑体验差 | 用纯文本编辑 JSON 键值对，没有结构化编辑 | 容易写错 JSON 格式 |
| **P1-4** | `MonitorController.Update` 没有对模板模式下 `TemplateVariablesJson` 为 null 的处理 | 前端传 `null` 时后端会接收 | 保存后清空变量 |

### 🔵 P2 — 后续优化

| # | 问题 | 原因 | 影响 |
|---|---|---|---|
| **P2-1** | 没有步骤级别的可视化 | 全部手动编辑 JSON | 模板步骤多时难以管理 |
| **P2-2** | 没有模板版本管理 | 无 | 无法回滚 |
| **P2-3** | 没有批量创建 | 无 `/apply` `/batch` 端点 | 测试大量模板效率低 |
| **P2-4** | 执行详情未与 Monitor.vue 对接 | 模板执行记录展示独立 | 用户需要在两个页面间切换 |

---

## 3. Phased Plan

### Phase 1：导入 + JSON 编辑器 + CRUD（当前）

**核心流程**：

```
用户操作                           系统行为
───────                          ────────
1. 点击「导入JSON文件」          弹出文件选择框，选 .json 文件
   └── 选择本地 template.json    读取文件内容，填入 DSL 编辑器
                                 清空/填充变量编辑器
                                 状态 = "未保存的新模板"

2. 编辑 DSL / 修改变量           实时编辑（不做保存）
   填写模板名称

3. 点击「创建模板」              POST /api/monitor (targetType=TEMPLATE)
                                 左侧新增条目，选中状态
                                 右侧切换为编辑模式

4. 点击左侧模板                   GET /api/monitor/{id}
                                 填充 DSL 编辑器 + 变量 + 名称

5. 修改后点击「保存」             PUT /api/monitor/{id}

6. 点击「运行模板」              POST /api/monitor/{id}/run
                                 3秒后查询执行记录
```

#### 前端改动

| 任务 | 文件 | 说明 |
|---|---|---|
| JSON 文件导入按钮 | `TemplateDesigner.vue` | `<input type="file" accept=".json">` + FileReader |
| JSON 格式合法性提示 | `TemplateDesigner.vue` | 保存前 `JSON.parse` 校验，无效则提示 |
| 创建/保存/删除/运行 | `TemplateDesigner.vue` | 对齐后端 API 返回字段 |
| 执行结果展示 | `TemplateDesigner.vue` | 执行后自动查询最新记录 |

#### 后端改动（无）

当前后端 API 已完整支持模板 CRUD，无需改动。

---

### Phase 2：步骤配置 UI 化

**目标**：用户通过表单配置步骤，而不是手写 JSON。

```
当前（Phase 1）：                         目标（Phase 2）：
┌─────────────────────────────┐          ┌─────────────────────────────┐
│ DSL 定义（JSON）            │          │ 步骤列表                    │
│                             │          │                             │
│ { "steps": [               │          │ ┌─ 1. login ──────────────┐ │
│   { "name": "login",      │          │ │ URL: {{host}}/api/login │ │
│     "type": "http",       │          │ │ Method: POST            │ │
│     "input": { ... }      │          │ │ └───────────────────────┘ │
│   }                       │          │ ┌─ 2. checkUser ──────────┐ │
│ ] }                       │          │ │ URL: {{host}}/api/user  │ │
│                             │          │ │ └───────────────────────┘ │
└─────────────────────────────┘          └─────────────────────────────┘
```

#### 前端改动

| 任务 | 说明 |
|---|---|
| 步骤列表组件 | 可拖拽排序的步骤列表，显示 name/type/status |
| 步骤编辑器弹窗 | 根据 type 显示不同的表单（HTTP URL/Method/Headers、DB ConnectionString/SQL） |
| 变量管理组件 | 结构化的键值对编辑器，支持批量导入/导出 |
| 并行组编辑器 | 拖拽步骤进入并行组，配置 mode/timeout |
| Extract 编辑器 | 可视化配置响应提取（JsonPath/Regex） |

#### 后端改动

| 任务 | 文件 | 改动 |
|---|---|---|
| 添加 DSL 的 JSON Schema 端点 | 新增 `GET /api/monitor/template-schema` | 返回支持的 type、字段等元数据 |
| 添加 DSL 语法验证端点 | 新增 `POST /api/monitor/template-validate` | 只校验不保存 |

---

### Phase 3：批量操作与版本管理

| 任务 | 说明 |
|---|---|
| 模板克隆 | 一键复制已有模板 |
| 批量创建 | `POST /api/monitor/template/batch` 导入多个模板 |
| 模板版本历史 | 每次保存生成版本快照，支持回滚 |
| 模板市场（可选） | 预置常用模板（API 健康检查、数据库连接测试等） |

---

## 4. 后端 API 对照表

### 已有 API（可直接复用）

| 方法 | 路径 | 权限 | 用途 | 备注 |
|---|---|---|---|---|
| `GET` | `/api/monitor/list` | `api.monitor.view` | 获取模板列表 | 返回 `isTemplate` 字段，前端过滤 |
| `GET` | `/api/monitor/{id}` | `api.monitor.view` | 获取模板详情 | 返回 `targetConfig`(DSL JSON)、`templateVariablesJson` |
| `POST` | `/api/monitor` | `api.monitor.create` | 创建模板 | `targetType: "TEMPLATE"` |
| `PUT` | `/api/monitor/{id}` | `api.monitor.update` | 更新模板 | 同上 |
| `DELETE` | `/api/monitor/{id}` | `api.monitor.delete` | 删除模板 | 软删除 |
| `POST` | `/api/monitor/{id}/run` | `api.monitor.run` | 运行模板 | 返回 `idempotencyKey` |
| `GET` | `/api/monitor/{id}/executions` | 无 | 执行记录列表 | 查询运行结果 |

### 建议新增 API

| 方法 | 路径 | 权限 | 用途 | 优先级 |
|---|---|---|---|---|
| `POST` | `/api/monitor/template/validate` | 无 | DSL JSON 语法校验 | P1 |
| `GET` | `/api/monitor/template/schema` | 无 | DSL Schema 定义 | P1 |
| `POST` | `/api/monitor/{id}/clone` | `api.monitor.create` | 克隆模板 | P2 |
| `POST` | `/api/monitor/template/batch` | `api.monitor.create` | 批量创建 | P3 |

---

## 5. 前端路由与权限

### 路由

```javascript
{ path: '/template', component: () => import('../views/TemplateDesigner.vue') }
```

### 权限

| 权限 Code | 用途 | 分配给 |
|---|---|---|
| `ui.menu.template` | 菜单显示 | admin（全部）、可按需分配给 user |
| `api.monitor.view` | 查看模板列表和详情 | admin + user |
| `api.monitor.create` | 创建模板 | admin |
| `api.monitor.update` | 更新模板 | admin |
| `api.monitor.delete` | 删除模板 | admin |
| `api.monitor.run` | 运行模板 | admin |

---

## 6. 数据流链路

### 创建模板

```
前端 TemplateDesigner.vue                   后端 MonitorController           后端 MonitorService
─────────────────────────                  ──────────────────────           ──────────────────
用户填写名称 + DSL JSON + 变量                         │                              │
         │                                            │                              │
POST /api/monitor                                    │                              │
{                                                    │                              │
  name: "健康检查",                                   │                              │
  targetType: "TEMPLATE",                            │                              │
  targetConfig: '{ "steps": [...] }',               │                              │
  isTemplate: true,                                  │                              │
  templateVariablesJson: '{ "host": "..." }',        │                              │
  assertions: [],                                    │                              │
  isEnabled: true                                    │                              │
}                                                    │                              │
         │                                            │                              │
         └──────────────────────────────────────────► │                              │
                                                     │  targetBuilder = TemplateTargetMap │
                                                     │  target = TemplateTarget(dslJson) │
                                                     │                              │
                                                     │  new MonitorEntity(           │
                                                     │    id, name, target, ...      │
                                                     │  )                            │
                                                     │  entity.SetTemplateConfig(    │
                                                     │    true, templateVariablesJson│
                                                     │  )                            │
                                                     │                              │
                                                     │  repository.AddAsync(entity)  │
                                                     │                              │
                                                     │◄──────── return id ──────────│
         ◄─────────── return Ok(id) ─────────────────│                              │
         │                                            │                              │
前端收到 id，selectedId = id                        │                              │
自动 loadDetail() 刷新页面                          │                              │
```

### 运行模板

```
前端                                           后端 Pipeline
───                                          ──────────
runTemplate()                                Orchestrator.TryExecuteAsync()
  │                                            │
  POST /api/monitor/{id}/run                   │
    └──────────────────────────────────────►   │
                                               │  Pipeline.ExecuteAsync(context)
                                               │    │
                                               │    ├── TemplateResolutionStep
                                               │    │   ├── 解析 DSL JSON
                                               │    │   ├── Schema 校验
                                               │    │   ├── 变量替换
                                               │    │   └── 构建 StepSequence
                                               │    │
                                               │    ├── RuntimeOrchestrationStep
                                               │    │   ├── 分布式锁 (dsl-run-{monitorId})
                                               │    │   ├── 串行步骤执行
                                               │    │   ├── 并行组执行
                                               │    │   ├── 变量提取 + 命名空间
                                               │    │   └── 重试/超时/降级
                                               │    │
                                               │    ├── ExecutionStep (跳过)
                                               │    │
                                               │    └── AssertionStep
                                               │        └── 对 DSL 最终结果断言
                                               │
                                               │  落库 ExecutionRecord
                                               │
    ◄──────── 返回 { idempotencyKey } ────────│
  │
  setTimeout(3s) → 查询执行结果
  GET /api/monitor/{id}/executions?take=5
```

---

## 7. 关键决策记录

### 决策 1：模板 = Monitor 的扩展，不是独立实体

**理由**：
- 复用已有的 CRUD（MonitorsApi.create/update/remove）
- 复用已有的执行链路（Orchestrator.TryExecuteAsync）
- 复用已有的权限体系（api.monitor.view/create/update/delete/run）
- 复用已有的执行记录（ExecutionRecord）
- 复用已有的断言引擎（AssertionStep）

代价：列表查询需要前端 `.filter(t => t.isTemplate)`，但这是很轻量的操作。

### 决策 2：DSL JSON 用文本域编辑，不做可视化

**Phase 1 理由**：
- 降低开发成本，快速可用
- JSON 格式对技术用户（测试人员）是可接受的
- 后端已有 Schema 校验，错误信息精确到 JSON Path

**Phase 2 计划**：
- 等 Phase 1 验证流程后，再投入开发可视化编辑器
- 可视化编辑器的本质是「JSON 编辑器的 UI 封装」，底层数据结构不变

### 决策 3：Pipeline 注册顺序固定，不动态发现

**理由**：
- 4 个步骤的顺序是固定的（TemplateResolution → RuntimeOrchestration → Execution → Assertion）
- 不需要动态编排，不需要用户配置
- 简单且可预测

### 决策 4：admin 全部权限 = 前端查全部 + 后端直通

**理由**：
- 前端：admin 返回全部 `SELECT Code FROM Permissions`，不查 RolePermissions 表
- 后端：`PermissionAuthorizationHandler` 中 admin 角色直通
- 新增权限零维护

---

## 附录 A：文件清单

### 后端

| 文件 | 状态 | 说明 |
|---|---|---|
| `src/AutoTest.Core/Target/Template/TemplateTarget.cs` | ✅ 已实现 | 模板目标类型 |
| `src/AutoTest.Application/Dto/TemplateTargetDto.cs` | ✅ 已实现 | 模板 DTO |
| `src/AutoTest.Infrastructure/Mapper/TargetMapper/TemplateTargetMap.cs` | ✅ 已实现 | 目标映射器 |
| `src/AutoTest.Application/Dto/MonitorDto.cs` | ✅ 已实现 | 扩展 `IsTemplate`、`TemplateVariablesJson` |
| `src/AutoTest.Core/Monitor/MonitorEntity.cs` | ✅ 已实现 | 扩展 `SetTemplateConfig()` |
| `src/AutoTest.Application/MonitorService.cs` | ✅ 已实现 | Add/Update 处理模板 |
| `src/AutoTest.Webapi/Controllers/MonitorController.cs` | ✅ 已实现 | List/Get 返回模板字段 |
| `src/AutoTest.Dsl/`（整个类库） | ✅ 已实现 | DSL 解析 + Schema 校验 |
| `src/AutoTest.Orchestration/`（整个类库） | ✅ 已实现 | Runtime 编排 |
| `src/AutoTest.Application/Step/ExecutionStep.cs` | ✅ 已修复 | IsTemplate 跳过 |
| `src/Auth/RBAC/Table/SeedRbacRefresh.cs` | ✅ 已实现 | 迁移 `2026050202` |

### 前端

| 文件 | 状态 | 说明 |
|---|---|---|
| `src/views/TemplateDesigner.vue` | ✅ 已创建 | 模板设计页面 |
| `src/router/index.js` | ✅ 已修改 | 添加 `/template` 路由 |
| `src/layout/Layout.vue` | ✅ 已修改 | 添加"模板设计"菜单 |
| `src/api/monitors.js` | ✅ 已有 | 复用 CRUD API |

---

## 附录 B：DSL JSON 格式参考

### 最小示例

```json
{
  "steps": [
    {
      "name": "checkHealth",
      "type": "http",
      "input": {
        "url": "{{host}}/api/health",
        "method": "Get",
        "timeout": 15
      },
      "assertions": [
        { "field": "StatusCode", "operator": "Equal", "expected": "200" }
      ]
    }
  ]
}
```

### 完整示例

```json
{
  "name": "用户注册流程测试",
  "steps": [
    {
      "name": "register",
      "type": "http",
      "input": {
        "url": "https://{{host}}/api/register",
        "method": "Post",
        "body": {
          "type": "Json",
          "value": {
            "username": "{{username}}",
            "password": "{{password}}"
          }
        },
        "headers": {
          "Content-Type": ["application/json"]
        },
        "timeout": 30
      },
      "extract": [
        {
          "name": "userId",
          "source": "Body",
          "method": "JsonPath",
          "expression": "$.data.userId"
        }
      ],
      "retry": {
        "count": 2,
        "delayMs": 1000,
        "backoff": "exponential"
      },
      "onFailure": "stop",
      "assertions": [
        { "field": "StatusCode", "operator": "Equal", "expected": "201" },
        { "field": "Body", "operator": "Contains", "expected": "success" }
      ]
    },
    {
      "name": "getProfile",
      "type": "http",
      "input": {
        "url": "https://{{host}}/api/users/{{userId}}",
        "method": "Get",
        "timeout": 15
      },
      "assertions": [
        { "field": "StatusCode", "operator": "Equal", "expected": "200" }
      ]
    }
  ],
  "parallel": [
    {
      "name": "checkServices",
      "steps": [
        {
          "name": "checkDB",
          "type": "db",
          "input": {
            "dbType": "sqlserver",
            "connectionString": "{{dbConn}}",
            "sql": "SELECT 1",
            "commandType": "Scalar"
          },
          "assertions": [
            { "field": "Scalar", "operator": "Equal", "expected": "1" }
          ]
        },
        {
          "name": "checkCache",
          "type": "tcp",
          "input": {
            "host": "{{redisHost}}",
            "port": 6379,
            "timeout": 5
          },
          "assertions": [
            { "field": "IsConnected", "operator": "Equal", "expected": "true" }
          ]
        }
      ],
      "mode": "all",
      "timeout": "30s"
    }
  ]
}
```

### 支持的字段一览

| 字段 | 类型 | 必填 | 说明 |
|---|---|---|---|
| `name` | string | 否 | DSL 名称 |
| `steps` | array | 是 | 步骤列表（按序执行） |
| `parallel` | array | 否 | 并行组列表 |
| `steps[].name` | string | 是 | 步骤名称 |
| `steps[].type` | string | 是 | 执行器类型：`http` / `tcp` / `db` / `python` |
| `steps[].input` | object | 是 | 执行器参数 |
| `steps[].extract` | array | 否 | 变量提取配置 |
| `steps[].retry` | object | 否 | 重试策略 |
| `steps[].timeout` | string | 否 | 步骤超时（如 `"30s"`） |
| `steps[].onFailure` | string | 否 | 失败策略：`stop` / `skip` / `ignore` |
| `steps[].assertions` | array | 否 | 步骤级断言 |
| `parallel[].mode` | string | 否 | `all` / `any` |

# HTTP 响应提取链 + 可执行模板 + 批量创建 — 实现计划

***

## 一、要解决的问题

### 问题 1：不能做多步骤请求链

当前 HTTP 监控只能发**一次请求**。无法实现"先登录取 Token → 再用 Token 调业务接口"这种最常见场景。

### 问题 2：配置无法复用

创建 10 个只 URL 不同的监控，需要手动写 10 份完整 JSON，没有模板机制。

### 问题 3：无变量填充

配置中的 URL / Token / Body 等字段不支持 `{{variable}}` 占位符，无法动态注入。

***

## 二、改动范围总览

```
HttpChainTarget (新)           ← 多步骤链目标
HttpChainStep (新)             ← 链中的单个步骤
HttpChainExecutionEngine (新)  ← 链执行引擎
IValueExtractor (新)           ← 响应值提取器 (JSONPath/Header/Regex)

MonitorTemplate (新)           ← 模板模型
IMonitorTemplateService (新)   ← 模板服务
ITemplateVariableResolver (新) ← 变量解析器

MonitorController 扩展         ← Batch API
HttpTargetMap 扩展             ← 支持模板映射
```

***

## 三、实施步骤

### Step 1: 提取器基础设施 (Core 层)

**新建文件**: `src/AutoTest.Core/Target/Http/ValueExtractor.cs`

```csharp
// 定义从 HTTP 响应中提取值的策略
public enum ExtractSource { Body, Header }
public enum ExtractMethod { JsonPath, Regex, Plain }

public class ValueExtractor
{
    public string Name { get; init; } = "";      // 变量名，如 "authToken"
    public ExtractSource Source { get; init; }
    public ExtractMethod Method { get; init; }
    public string Expression { get; init; } = ""; // JSONPath 或 Regex
}

// 解析器实现
public interface IResponseValueExtractor
{
    Task<Dictionary<string, string>> ExtractAsync(
        string body,
        IReadOnlyDictionary<string, string[]> headers,
        List<ValueExtractor> extractors);
}
```

**新建文件**: `src/AutoTest.Infrastructure/Target/Http/ResponseValueExtractor.cs`

* 使用 Newtonsoft.Json 或 System.Text.Json + 简单 JSONPath 实现（$..token、$.data.token 等）

* 支持 Header 提取（`header:Set-Cookie`）

* 支持正则提取（`regex:access_token=(\\w+)`）

***

### Step 2: 变量解析器 (Core 层)

**新建文件**: `src/AutoTest.Core/Target/Http/VariableResolver.cs`

```csharp
public interface ITemplateVariableResolver
{
    // 把 input 中的 {{varName}} 替换为 variables[varName] 的值
    string Resolve(string input, Dictionary<string, string> variables);

    // 对 HttpTarget 做全字段变量替换
    HttpTarget ResolveTarget(HttpTarget target, Dictionary<string, string> variables);
}
```

支持字段：

* `url` → `https://{{host}}/api/{{path}}`

* `body.value` → JSON 中的 `{{token}}`

* `authToken` → `{{loginToken}}`

* `headers` 中的 value

* `query` 中的 value

***

### Step 3: 链目标模型 (Core 层)

**新建文件**: `src/AutoTest.Core/Target/Http/HttpChainTarget.cs`

```csharp
public class HttpChainTarget : MonitorTarget
{
    public override string Type => "HttpChain";

    public List<HttpChainStep> Steps { get; init; } = new();
}

public class HttpChainStep
{
    public string Name { get; init; } = "";          // 步骤名，如 "login"
    public HttpTarget Request { get; init; } = null!; // 请求配置
    public List<ValueExtractor> Extract { get; init; } = new(); // 从响应中提取变量
    public List<AssertionRule>? Assertions { get; init; } // 本步骤断言（可选）
}
```

**修改文件**: `src/AutoTest.Core/Monitor/MonitorEntity.cs`

* `MonitorEntity` 的 `MonitorTarget` 可以是 `HttpTarget` 或 `HttpChainTarget`

* 无需代码修改，`MonitorTarget` 本身就是抽象基类

***

### Step 4: 链执行引擎 (Execution 层)

**新建文件**: `src/AutoTest.Execution/Http/HttpChainExecutionEngine.cs`

```csharp
public class HttpChainExecutionEngine : IExecutionEngine
{
    public bool CanExecute(MonitorTarget target) => target is HttpChainTarget;

    public async Task<ExecutionResult> ExecuteAsync(MonitorTarget target)
    {
        var chain = (HttpChainTarget)target;
        var variables = new Dictionary<string, string>();
        var stepResults = new List<StepResult>();

        foreach (var step in chain.Steps)
        {
            // 1. 变量替换：将 {{var}} 替换为当前 variables 中的值
            var resolved = ResolveStep(step, variables);

            // 2. 执行请求
            var result = await _httpEngine.ExecuteAsync(resolved.Request);

            // 3. 如果该步骤有提取器，从响应中提取变量
            if (step.Extract?.Count > 0 && result.IsExecutionSuccess)
            {
                var extracted = await _extractor.ExtractAsync(
                    result.Body, result.Headers, step.Extract);
                foreach (var kv in extracted)
                    variables[kv.Key] = kv.Value;
            }

            stepResults.Add(new StepResult(step.Name, result));

            // 4. 如果步骤有断言且失败，提前终止
            if (!result.IsExecutionSuccess && step.Assertions?.Count > 0)
                break;
        }

        // 聚合结果：返回最后一步的结果 + 链上下文
        var last = stepResults.Last().Result;
        return new HttpChainExecutionResult(last.StatusCode, last.Body, last.IsExecutionSuccess,
            last.Headers, last.ElapsedMilliseconds ?? 0, stepResults, variables);
    }
}
```

**新建文件**: `src/AutoTest.Execution/Http/HttpChainExecutionResult.cs`

* 继承 `ExecutionResult`，带 `StepResults` 列表和最终 `Variables` 快照

***

### Step 5: 模板模型 (Core 层)

**新建文件**: `src/AutoTest.Core/AI/MonitorTemplate.cs`

```csharp
public class MonitorTemplate
{
    public Guid Id { get; init; }
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";

    // 模板定义本身 — 包含 {{var}} 占位符
    public string TargetType { get; init; } = "";
    public string TargetConfigJson { get; init; } = ""; // 含 {{var}}

    // 模板级别的断言配置（也含 {{var}}）
    public string AssertionsConfigJson { get; init; } = "";

    // 变量定义（描述模板有哪些变量需要填）
    public List<TemplateVariableDef> Variables { get; init; } = new();

    public DateTime CreatedAt { get; init; }
}

public class TemplateVariableDef
{
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public string DefaultValue { get; init; } = "";
    public bool Required { get; init; } = true;
}
```

***

### Step 6: 模板服务 (Application 层)

**新建文件**: `src/AutoTest.Application/IMonitorTemplateService.cs`

```csharp
public interface IMonitorTemplateService
{
    // CRUD 模板
    Task<Guid> CreateTemplateAsync(MonitorTemplate template);
    Task<MonitorTemplate?> GetTemplateAsync(Guid id);
    Task<List<MonitorTemplate>> ListTemplatesAsync();

    // 从模板创建单个监控
    Task<Guid> CreateMonitorFromTemplateAsync(
        Guid templateId,
        Dictionary<string, string> variables,
        string monitorName);

    // 从模板批量创建监控
    Task<List<Guid>> BatchCreateMonitorsFromTemplateAsync(
        Guid templateId,
        List<Dictionary<string, string>> variableSets,
        string? namePrefix = null);
}
```

**新建文件（或扩展）**: `src/AutoTest.Application/MonitorTemplateService.cs`

* `CreateMonitorFromTemplateAsync`: 变量替换 → 调用 `MonitorService.AddAsync`

* `BatchCreateMonitorsFromTemplateAsync`: 循环调用 CreateMonitorFromTemplateAsync（事务包装）

***

### Step 7: 模板存储 (Infrastructure 层)

**新建文件**: `src/AutoTest.Core/Repositories/IMonitorTemplateRepository.cs`

```csharp
public interface IMonitorTemplateRepository
{
    Task AddAsync(MonitorTemplate template, IDbTransaction? tx = null);
    Task<MonitorTemplate?> GetByIdAsync(Guid id);
    Task<List<MonitorTemplate>> ListAsync();
    Task DeleteAsync(Guid id, IDbTransaction? tx = null);
}
```

**新建文件**: `src/AutoTest.Infrastructure/MonitorTemplateRepository.cs`

* Dapper 实现

* 新增 `MonitorTemplate` 表（FluentMigrator）

***

### Step 8: HTTP Chain Target 的 TargetMap 和 DI 注册

**修改**: `src/AutoTest.Infrastructure/Mapper/TargetMapper/HttpTargetMap.cs`

* 增加 `HttpChain` 类型的映射支持

**修改**: `src/AutoTest.Execution/Http/AddExecutionHttp.cs`

* 注册 `HttpChainExecutionEngine`

**修改**: `src/AutoTest.Application/ApplicationServiceCollectionExtensions.cs`

* 注册 `IMonitorTemplateService`

**修改**: `src/AutoTest.Infrastructure/AddInfrastructureServiceCollectionExtensions.cs`

* 注册 `IMonitorTemplateRepository`

* 注册 `IResponseValueExtractor`

***

### Step 9: 批量创建 API (Webapi)

**修改**: `src/AutoTest.Webapi/Controllers/MonitorController.cs`

新增端点：

```csharp
// 模板 CRUD
POST   /api/monitor/templates                    → CreateTemplate
GET    /api/monitor/templates                    → ListTemplates
GET    /api/monitor/templates/{id}               → GetTemplate
DELETE /api/monitor/templates/{id}               → DeleteTemplate

// 基于模板创建监控
POST   /api/monitor/templates/{id}/apply         → 单次应用（传入 variables）
POST   /api/monitor/templates/{id}/batch         → 批量应用（传入 variableSets[]）
```

**请求体示例（Batch）**：

```json
{
  "namePrefix": "生产环境-数据中心A",
  "variableSets": [
    {
      "host": "dc1-api.example.com",
      "path": "health",
      "expectedStatus": "200"
    },
    {
      "host": "dc1-api.example.com",
      "path": "users",
      "expectedStatus": "200"
    }
  ]
}
```

***

### Step 10: MonitorTemplate 数据库迁移

**新建文件**: `src/AutoTest.Migrations/CreateMonitorTemplateTable.cs`

```sql
CREATE TABLE MonitorTemplate (
    Id              UNIQUEIDENTIFIER PRIMARY KEY,
    Name            NVARCHAR(256)    NOT NULL,
    Description     NVARCHAR(1024)   NULL,
    TargetType      NVARCHAR(64)     NOT NULL,
    TargetConfigJson NVARCHAR(MAX)   NOT NULL,
    AssertionsConfigJson NVARCHAR(MAX) NULL,
    VariablesJson   NVARCHAR(MAX)    NOT NULL,
    CreatedAt       DATETIME2        NOT NULL
);
```

***

## 四、依赖关系

```
Step 1 (ValueExtractor)     ← 无依赖
Step 2 (VariableResolver)   ← 无依赖
Step 3 (HttpChainTarget)    ← 依赖 Step 1
Step 4 (ChainEngine)        ← 依赖 Step 3
Step 5 (MonitorTemplate)    ← 无依赖
Step 6 (TemplateService)    ← 依赖 Step 2, 5
Step 7 (TemplateRepo)       ← 依赖 Step 5
Step 8 (DI/Map)             ← 依赖 Step 3, 4, 6, 7
Step 9 (API)                ← 依赖 Step 8
Step 10 (Migration)         ← 依赖 Step 5
```

**执行顺序**: Step 1 → 2 → 3 → 5 → 4 → 7 → 6 → 10 → 8 → 9

***

## 五、模板使用示例

### 定义模板

```json
{
  "name": "API 健康检查模板",
  "description": "通用的 HTTP API 健康检查模板",
  "targetType": "HTTP",
  "targetConfigJson": "{
    \"url\": \"https://{{host}}/{{path}}\",
    \"method\": \"Get\",
    \"timeout\": {{timeout}},
    \"headers\": {
      \"Authorization\": [\"Bearer {{authToken}}\"]
    }
  }",
  "assertionsConfigJson": "[
    { \"type\": \"Http\", \"configJson\": \"{\\\"field\\\": \\\"StatusCode\\\", \\\"operator\\\": \\\"Equal\\\", \\\"expected\\\": \\\"{{expectedStatus}}\\\"}\" }
  ]",
  "variables": [
    { "name": "host", "description": "API 地址", "required": true },
    { "name": "path", "description": "API 路径", "required": true },
    { "name": "timeout", "description": "超时秒数", "defaultValue": "30" },
    { "name": "authToken", "description": "认证 Token", "required": false },
    { "name": "expectedStatus", "description": "预期状态码", "defaultValue": "200" }
  ]
}
```

### 链式请求模板（先登录再取数据）

```json
{
  "name": "登录后查询用户信息",
  "targetType": "HttpChain",
  "targetConfigJson": "{
    \"steps\": [
      {
        \"name\": \"login\",
        \"request\": {
          \"url\": \"https://{{host}}/api/login\",
          \"method\": \"Post\",
          \"body\": {
            \"type\": \"Json\",
            \"value\": { \"username\": \"{{username}}\", \"password\": \"{{password}}\" }
          }
        },
        \"extract\": [
          { \"name\": \"sessionToken\", \"source\": \"Body\", \"method\": \"JsonPath\", \"expression\": \"$.data.token\" },
          { \"name\": \"userId\", \"source\": \"Body\", \"method\": \"JsonPath\", \"expression\": \"$.data.userId\" }
        ]
      },
      {
        \"name\": \"getProfile\",
        \"request\": {
          \"url\": \"https://{{host}}/api/users/{{userId}}/profile\",
          \"method\": \"Get\",
          \"headers\": { \"Authorization\": [\"Bearer {{sessionToken}}\"] }
        }
      }
    ]
  }",
  "variables": [
    { "name": "host", "description": "API 地址", "required": true },
    { "name": "username", "description": "登录用户名", "required": true },
    { "name": "password", "description": "登录密码", "required": true }
  ]
}
```

### 批量创建

```json
POST /api/monitor/templates/{templateId}/batch
{
  "namePrefix": "生产环境",
  "variableSets": [
    { "host": "dc1.example.com", "path": "health", "expectedStatus": "200" },
    { "host": "dc2.example.com", "path": "health", "expectedStatus": "200" },
    { "host": "dc3.example.com", "path": "health", "expectedStatus": "200" }
  ]
}
```

→ 一次创建 3 个监控，分别监控 dc1/dc2/dc3 的健康检查。

***

## 六、无需改动的基础设施

以下部分保持原样，不受本次改动影响：

| 模块                    | 原因                                   |
| --------------------- | ------------------------------------ |
| `AiWorker` / `AiTask` | 链执行的失败事件仍走 Outbox → AI 分析            |
| `ExecutionRecord`     | 记录最后一次执行结果                           |
| `Orchestrator`        | 对 `MonitorTarget` 类型无感知              |
| `HttpExecutionEngine` | 链引擎内部复用 `HttpExecutionEngine` 执行单个步骤 |


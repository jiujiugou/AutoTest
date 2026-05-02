# 模板匹配架构 — 设计文档

> 版本: v1.0 | 状态: Draft

---

## 1. 为什么需要模板匹配

### 当前问题

AutoTest 的执行流水线目前是：

```
执行 (ExecutionStep) → 断言 (AssertionStep)
```

这个模式假设 `MonitorEntity.Target` 是**完全解析好的、可直接执行的配置**。但真实场景中，用户需要处理动态变量：

- **场景 A**：想用一个配置模板监控 10 个不同数据中心的同一个接口，只有 host 不同
- **场景 B**：需要先请求登录接口，从响应中提取 Token，再用这个 Token 请求业务接口
- **场景 C**：以上两者的组合——模板 + 链式请求

当前方案要求用户在 API 层自己完成变量替换，或者在 `HttpTarget` 中硬编码所有值——既不灵活也不可复用。

### 解决方案

在流水线最前面增加一个**模板解析步骤**，把"含占位符的模板"解析为"可执行的目标"：

```
模板解析 (TemplateResolutionStep) → 执行 (ExecutionStep) → 断言 (AssertionStep)
```

模板解析是一个**顶层抽象**，每种目标类型（HTTP / TCP / DB / Chain）有自己的解析器实现。

---

## 2. 整体架构

```
┌─────────────────────────────────────────────────────────────┐
│                         Pipeline                            │
│  ┌──────────────────┐  ┌──────────────┐  ┌──────────────┐  │
│  │TemplateResolution│  │ ExecutionStep │  │ AssertionStep │  │
│  │     Step         │─▶│              │─▶│              │  │
│  └────────┬─────────┘  └──────────────┘  └──────────────┘  │
│           │                                                  │
│           │ 按 TargetType 路由到对应的 Matcher                │
│           ▼                                                  │
└─────────────────────────────────────────────────────────────┘

         ITemplateMatcher (顶层抽象)
         ├── HttpTemplateMatcher    — 替换 {{var}} 占位符
         ├── TcpTemplateMatcher     — 替换 {{var}} 占位符
         ├── DbTemplateMatcher      — 替换 {{var}} 占位符
         └── ChainTemplateMatcher   — 多步骤解析 + 响应变量提取
```

### 每个 Matcher 的职责

| Matcher | 输入 | 输出 | 核心逻辑 |
|---------|------|------|----------|
| `HttpTemplateMatcher` | 含 `{{var}}` 的 HttpTarget JSON + 变量字典 | 已解析的 `HttpTarget` | 字符串替换 `{{var}}` → 变量值 |
| `TcpTemplateMatcher` | 含 `{{var}}` 的 TcpTarget JSON + 变量字典 | 已解析的 `TcpTarget` | 同上 |
| `DbTemplateMatcher` | 含 `{{var}}` 的 DbTarget JSON + 变量字典 | 已解析的 `DbTarget` | 同上 |
| `ChainTemplateMatcher` | ChainTarget 定义 + 变量字典 | 最后一步的 `ExecutionResult` | 循环解析每个步骤，步骤间传播响应提取的变量 |

---

## 3. 核心模型变化

### 3.1 MonitorEntity 新增字段

```csharp
public class MonitorEntity
{
    // ... 现有字段不变 ...

    /// <summary>
    /// 模板变量 JSON。存的是 {"host":"dc1.example.com","port":"8080"}
    /// 为 null 表示该监控不使用模板，Target 是直接可执行的
    /// </summary>
    public string? TemplateVariablesJson { get; private set; }

    /// <summary>
    /// 标记当前 Target 是否是模板（含 {{var}} 占位符）
    /// true  → 执行前需要经过 TemplateResolutionStep
    /// false → 直接执行（兼容现有行为）
    /// </summary>
    public bool IsTemplate { get; private set; }
}
```

**设计原则**：
- `IsTemplate = false` 的监控行为完全不变，不破坏现有数据
- `Target` 始终存配置（不论是否含占位符），`TemplateVariablesJson` 存变量 KV
- 批量创建时：每组变量创建一个 `MonitorEntity`，共享同一个 `Target`（模板）

### 3.2 TemplateResolutionResult

```csharp
public class TemplateResolutionResult
{
    /// <summary>
    /// 解析后的可执行目标
    /// </summary>
    public MonitorTarget ResolvedTarget { get; init; } = null!;

    /// <summary>
    /// 解析过程中从响应提取的新变量
    /// （仅 ChainTemplateMatcher 会产出，单步 Matcher 为空）
    /// </summary>
    public Dictionary<string, string> ExtractedVariables { get; init; } = new();
}
```

---

## 4. 模板解析步骤设计

### ITemplateMatcher 接口（Core 层）

```csharp
public interface ITemplateMatcher
{
    /// <summary>
    /// 判断当前解析器是否能处理指定类型的模板
    /// </summary>
    bool CanHandle(string targetType);

    /// <summary>
    /// 解析模板：将含 {{var}} 的配置 + 变量字典 → 解析后的目标
    /// </summary>
    /// <param name="templateJson">含 {{var}} 的 TargetConfig JSON</param>
    /// <param name="variables">变量字典</param>
    /// <returns>解析结果</returns>
    Task<TemplateResolutionResult> ResolveAsync(
        string templateJson,
        Dictionary<string, string> variables);
}
```

### TemplateResolutionStep（新增 Pipeline 步骤）

```csharp
public class TemplateResolutionStep : IPipelineStep
{
    private readonly IEnumerable<ITemplateMatcher> _matchers;

    public async Task InvokeAsync(PipelineContext context, Func<Task> next)
    {
        // 如果当前监控不是模板模式，跳过
        if (!context.Monitor.IsTemplate)
        {
            await next();
            return;
        }

        // 找到对应的 Matcher
        var matcher = _matchers.First(m => m.CanHandle(context.Monitor.Target.Type));

        // 解析变量
        var variables = JsonSerializer.Deserialize<Dictionary<string, string>>(
            context.Monitor.TemplateVariablesJson ?? "{}")!;

        var result = await matcher.ResolveAsync(
            context.Monitor.Target.ToJson(),
            variables);

        // 将解析后的目标存回上下文，传给 ExecutionStep
        context.Items["ResolvedTarget"] = result.ResolvedTarget;
        context.Items["ExtractedVariables"] = result.ExtractedVariables;

        await next();
    }
}
```

### ExecutionStep 的适配

```csharp
public class ExecutionStep : IPipelineStep
{
    public async Task InvokeAsync(PipelineContext context, Func<Task> next)
    {
        // 如果上下文中有解析后的目标，优先使用
        var target = context.Items.TryGetValue("ResolvedTarget", out var rt)
            ? (MonitorTarget)rt
            : context.Monitor.Target;

        var engine = _engineResolver.Resolve(target);
        context.Result = await engine.ExecuteAsync(target);

        await next();
    }
}
```

**这样改动最小** — `ExecutionStep` 只加了一行 if 判断式逻辑。

---

## 5. 各 Matcher 实现

### 5.1 HttpTemplateMatcher

```csharp
public class HttpTemplateMatcher : ITemplateMatcher
{
    public bool CanHandle(string targetType) => targetType == "HTTP";

    public Task<TemplateResolutionResult> ResolveAsync(string templateJson, Dictionary<string, string> variables)
    {
        // 把 templateJson 中的 {{var}} 全部替换为 variables[var]
        var resolvedJson = VariableResolver.Replace(templateJson, variables);
        var target = JsonSerializer.Deserialize<HttpTarget>(resolvedJson)!;
        return Task.FromResult(new TemplateResolutionResult { ResolvedTarget = target });
    }
}
```

### 5.2 ChainTemplateMatcher

核心流程：

```
输入: ChainTarget.Steps[] + 初始变量字典
                    │
         ┌──────────▼──────────┐
         │ 遍历每个 Step       │
         │                     │
         │ 1. 变量替换 Step    │
         │    ({{var}} → 值)   │
         │                     │
         │ 2. 执行 Step        │
         │    (复用 HttpExecutionEngine) │
         │                     │
         │ 3. 从响应提取变量    │
         │    (JSONPath/Header)│
         │    → 追加到变量池    │
         │                     │
         │ 4. Step 有断言?     │
         │    → 评估断言       │
         │    → 失败则终止    │
         └──────────┬──────────┘
                    │
         ┌──────────▼──────────┐
         │ 返回最后一步的      │
         │ ExecutionResult     │
         │ + 所有步骤上下文    │
         └─────────────────────┘
```

```csharp
public class ChainTemplateMatcher : ITemplateMatcher
{
    public bool CanHandle(string targetType) => targetType == "HttpChain";

    public async Task<TemplateResolutionResult> ResolveAsync(
        string templateJson, Dictionary<string, string> variables)
    {
        var chain = JsonSerializer.Deserialize<HttpChainTarget>(templateJson)!;
        var resolvedTarget = chain.Steps.Last().Request; // 最后一步作为最终目标

        foreach (var step in chain.Steps)
        {
            // 变量替换
            var stepJson = step.Request.ToJson();
            var resolvedJson = VariableResolver.Replace(stepJson, variables);
            var resolvedStep = JsonSerializer.Deserialize<HttpTarget>(resolvedJson)!;

            // 执行
            var result = await _httpEngine.ExecuteAsync(resolvedStep);

            // 提取变量
            if (step.Extract?.Count > 0 && result.IsExecutionSuccess)
            {
                var extracted = await _extractor.ExtractAsync(
                    result.Body, result.Headers, step.Extract);
                foreach (var kv in extracted)
                    variables[kv.Key] = kv.Value;
            }

            // 步骤断言失败 → 终止
            if (!result.IsExecutionSuccess)
            {
                resolvedTarget = resolvedStep; // 用失败的步骤作为返回
                break;
            }
        }

        return new TemplateResolutionResult
        {
            ResolvedTarget = resolvedTarget, // 最后成功或失败的步骤
            ExtractedVariables = variables
        };
    }
}
```

**链式请求的断言归谁管？** —— 每个步骤可以有自己的步骤级断言（`Step.Assertions`），用于提前终止；**最终断言由 Pipeline 的 AssertionStep 统一处理**，作用在最后一步的结果上。

---

## 6. TemplateVariableResolver — 变量解析工具

```csharp
public static class VariableResolver
{
    /// <summary>
    /// 替换字符串中所有 {{name}} 占位符
    /// 支持默认值语法：{{name:defaultValue}}
    /// </summary>
    public static string Replace(string input, Dictionary<string, string> variables)
    {
        return Regex.Replace(input, @"\{\{(\w+)(?::([^}]*))?\}\}", match =>
        {
            var name = match.Groups[1].Value;
            var defaultValue = match.Groups[2].Success ? match.Groups[2].Value : null;

            if (variables.TryGetValue(name, out var value))
                return value;

            if (defaultValue != null)
                return defaultValue;

            throw new InvalidOperationException($"模板变量 {name} 未提供值且无默认值");
        });
    }

    /// <summary>
    /// 替换 JSON 对象中所有字符串字段的 {{var}}
    /// </summary>
    public static string ReplaceJsonValues(string json, Dictionary<string, string> variables)
    {
        // 遍历 JSON 的所有 string value 节点，对每个 value 执行 Replace
        // 使用 System.Text.Json 的 JsonDocument + Utf8JsonWriter
    }
}
```

**支持的占位符语法**：

| 语法 | 说明 | 示例 |
|------|------|------|
| `{{host}}` | 必须提供变量 host | `"url": "https://{{host}}/api/health"` |
| `{{timeout:30}}` | 可选变量，默认值 30 | `"timeout": {{timeout:30}}` |
| `{{authToken:}}` | 可选变量，默认空字符串 | `"authToken": "{{authToken:}}"` |

---

## 7. 批量创建 API

### 创建模板

```json
POST /api/monitor/templates
{
  "name": "API 健康检查",
  "targetType": "HTTP",
  "targetConfigJson": "{
    \"url\": \"https://{{host}}/{{path}}\",
    \"method\": \"Get\",
    \"timeout\": {{timeout:30}}
  }",
  "assertionsConfigJson": "[
    { \"type\": \"Http\", \"configJson\": \"{\\\"field\\\": \\\"StatusCode\\\", \\\"operator\\\": \\\"Equal\\\", \\\"expected\\\": \\\"{{expectedStatus:200}}\\\"}\" }
  ]"
}
```

### 从模板创建单个监控

```json
POST /api/monitor/templates/{templateId}/apply
{
  "name": "DC1-健康检查",
  "variables": {
    "host": "dc1.example.com",
    "path": "health"
  }
}
```

### 从模板批量创建监控

```json
POST /api/monitor/templates/{templateId}/batch
{
  "namePrefix": "生产环境",
  "variableSets": [
    { "host": "dc1.example.com", "path": "health" },
    { "host": "dc2.example.com", "path": "health" },
    { "host": "dc3.example.com", "path": "health" }
  ]
}
```

后台逻辑：

```
对 variableSets 中的每个条目：
  1. 变量替换 targetConfigJson → 完全的 HttpTarget JSON
  2. 变量替换 assertionsConfigJson → 完全的断言 JSON
  3. 创建 MonitorEntity:
       Target = 反序列化后的 HttpTarget（含占位符的原始模板）
       TemplateVariablesJson = {"host":"dc1.example.com", ...}
       IsTemplate = true
  4. 持久化
```

---

## 8. 数据库变化

### MonitorEntity 存储

| 字段 | 存什么 | 说明 |
|------|--------|------|
| `TargetConfig` (已有) | `{"url":"https://{{host}}/path","method":"Get"}` | 含占位符的原始模板 |
| `TemplateVariablesJson` (新增) | `{"host":"dc1.example.com"}` | 变量 KV |
| `IsTemplate` (新增) | `true` | 标记需要模板解析 |

**兼容性**：现有数据的 `IsTemplate = false`、`TemplateVariablesJson = null`，行为完全不变。

### MonitorTemplate 表

```sql
CREATE TABLE MonitorTemplate (
    Id                  UNIQUEIDENTIFIER PRIMARY KEY,
    Name                NVARCHAR(256)    NOT NULL,
    Description         NVARCHAR(1024)   NULL,
    TargetType          NVARCHAR(64)     NOT NULL,
    TargetConfigJson    NVARCHAR(MAX)    NOT NULL,
    AssertionsConfigJson NVARCHAR(MAX)   NULL,
    VariablesDefJson    NVARCHAR(MAX)    NOT NULL,  -- 变量定义的 JSON 数组
    CreatedAt           DATETIME2        NOT NULL
);
```

---

## 9. 不变的部分

| 模块 | 不受影响的原因 |
|------|--------------|
| `AiWorker` / `AiAnalysisConsumer` | 链执行失败事件仍走 Outbox → AI 分析，分析器只关心最终错误 |
| `ExecutionRecord` | 只记录 ExecutionStep 产出的结果，不关心是否经过模板解析 |
| `Orchestrator` | Pipeline 容器，对步骤内部逻辑无感知 |
| `HttpExecutionEngine` | ChainTemplateMatcher 内部复用，引擎本身不变 |
| 所有断言实现 | AssertionStep 收到的是已解析目标的执行结果，断言逻辑不变 |
| 现有监控任务 | `IsTemplate=false` 跳过 TemplateResolutionStep，行为零变化 |

---

## 10. 执行流程全景

### 普通监控（现有）

```
MonitorEntity.IsTemplate = false
MonitorEntity.Target = HttpTarget (完全解析)

Pipeline:
  TemplateResolutionStep → 跳过（IsTemplate=false）
  ExecutionStep → 直接用 Target 执行
  AssertionStep → 对结果断言
```

### 模板监控（新增）

```
MonitorEntity.IsTemplate = true
MonitorEntity.Target = HttpTarget (含 {{var}})
MonitorEntity.TemplateVariablesJson = {"host":"dc1.example.com"}

Pipeline:
  TemplateResolutionStep → HttpTemplateMatcher 替换 {{var}} → 已解析的 HttpTarget
  ExecutionStep → 用解析后的 Target 执行
  AssertionStep → 对结果断言
```

### 链式请求监控（新增）

```
MonitorEntity.IsTemplate = true
MonitorEntity.Target = ChainTarget (多步骤)

Pipeline:
  TemplateResolutionStep → ChainTemplateMatcher
                            ├── 步骤 1: 解析 → 执行 → 提取 Token → 追加到变量池
                            ├── 步骤 2: 解析(含上一步Token) → 执行 → 提取 → ...
                            └── 返回最后一步的 ResolvedTarget + 全变量池
  ExecutionStep → 用最后一步的目标执行（ChainTemplateMatcher 已执行，但幂等无副作用）
  AssertionStep → 对最后一步结果断言
```

---

## 11. 实现路径

| 步骤 | 内容 | 依赖 |
|------|------|------|
| 1 | 定义 `ITemplateMatcher` 接口 + `TemplateResolutionResult` | Core 层，无依赖 |
| 2 | 实现 `VariableResolver` 工具类 | Core 层，无依赖 |
| 3 | `MonitorEntity` 新增 `IsTemplate` / `TemplateVariablesJson` | Core 层 |
| 4 | 实现 `HttpTemplateMatcher` | Infrastructure 层，依赖 1+2 |
| 5 | 实现 `TemplateResolutionStep` | Application 层，依赖 1+3+4 |
| 6 | `ExecutionStep` 适配 `ResolvedTarget` | Application 层 |
| 7 | 实现 `ChainTemplateMatcher` + 提取器 | Infrastructure 层，依赖 1+2+4 |
| 8 | 定义 `MonitorTemplate` 模型 + 仓储 + Service | Core + Application + Infrastructure |
| 9 | 批量创建 API（`/apply` / `/batch`） | Webapi 层 |
| 10 | FluentMigrator 迁移 | Migrations 层 |
| 11 | 更新 http-usage.md 文档 | docs 层 |

# 基于 JSON 的模板 DSL 架构

> 版本: v4.1 | 状态: Draft

---

## 1. 设计思想

### 核心原则

1. **Monitor 本身就是模板载体**，`MonitorEntity` 加两个字段即可，不引入独立模型
2. **Step = `{ name, type, input }`**，没有领域子类。JSON 定义，运行时直接读取
3. **四阶段分离**：DSL 解析 → Runtime 编排 → Executor 执行 → Assertion 断言，各阶段通过 Pipeline 串联
4. **接口下沉到 Core**：`IPipelineStep` / `IPipeline` / `PipelineContext` 定义在 `AutoTest.Core`，避免 Dsl/Orchestration 对 Application 的依赖
5. **分布式锁统一放入 CacheCommons**（含 Redis 实现），现有缓存抽象已有防穿透/防击穿等能力
6. **执行器插件化**，每种 `type` 对应一个 `IStepExecutor`，现有 Engine 直接适配

### 模块定位

```
AutoTest.Dsl/               ← DSL 解析（Schema 校验 + 变量替换 + StepSequence 构建）
AutoTest.Orchestration/     ← Runtime 编排（上下文管理 + 步骤调度 + 重试/降级）

common/
├── CacheCommons/           ← ICacheService（已有）+ IDistributedLock（Redis 实现）
│                               缓存防穿透/防击穿/防雪崩（已有）
└── LockCommons/            ← 可选拆分：单独锁类库（如果锁逻辑膨胀的话）

AutoTest.Core/              ← IPipelineStep / IPipeline / PipelineContext / StepSequence / IStepExecutor 等接口和模型定义
AutoTest.Execution/         ← HttpStepExecutor / TcpStepExecutor / DbStepExecutor（适配层）
AutoTest.Application/       ← Pipeline 实现 + Orchestrator + ExecutionStep + AssertionStep
```

---

## 2. Pipeline 流程

```
                         Pipeline
                            │
              ┌─────────────┼─────────────┐
              ▼             ▼             ▼
        DSL解析          Runtime        Assertion
      (New Step)      (New Step)       (已有 Step)
                            │
                     ┌──────┴──────┐
                     ▼              ▼
              Executor执行     Executor执行
              (Step 1)         (Step N)
```

### Pipeline 步骤注册顺序

各步骤分别在各自类库的扩展方法中注册：

```csharp
// DslServiceCollectionExtensions.cs (AutoTest.Dsl)
services.AddScoped<IPipelineStep, TemplateResolutionStep>();

// OrchestrationServiceCollectionExtensions.cs (AutoTest.Orchestration)
services.AddScoped<IPipelineStep, RuntimeOrchestrationStep>();

// ApplicationServiceCollectionExtensions.cs (AutoTest.Application)
services.AddScoped<IPipelineStep, ExecutionStep>();
services.AddScoped<IPipelineStep, AssertionStep>();
```

> ⚠ **注意**：`IPipelineStep` 接口定义在 `AutoTest.Core.ExecutionPipeline`，各项目只需要引用 Core，不会产生循环依赖。

### 各阶段在 Pipeline 中的职责

| Step | 职责 | 所在类库 | IsTemplate=true | IsTemplate=false |
|------|------|---------|----------------|-----------------|
| **TemplateResolutionStep** | JSON 加载、Schema 校验、变量替换、构建 StepSequence | `AutoTest.Dsl` | 执行 | 跳过 |
| **RuntimeOrchestrationStep** | 步骤调度、重试、降级、分布式锁、进度追踪 | `AutoTest.Orchestration` | 执行 | 跳过 |
| **ExecutionStep** | 按 type 路由到执行引擎 | `AutoTest.Application` | 跳过（结果已在 RuntimeStep 中产生） | 正常执行 |
| **AssertionStep** | 断言评估 | `AutoTest.Application` | 对 DSL 最终结果断言 | 正常执行 |

---

## 3. Step 数据结构

### 定义

```json
{
  "name": "这组并行步骤的名称",
  "steps": [
    {
      "name": "login",
      "type": "http",
      "input": {
        "url": "https://{{host}}/api/login",
        "method": "Post",
        "body": {
          "type": "Json",
          "value": { "username": "{{username}}", "password": "{{password}}" }
        },
        "headers": { "Accept": ["application/json"] },
        "timeout": 15
      },
      "extract": [
        { "name": "token", "source": "Body", "method": "JsonPath", "expression": "$.data.token" }
      ],
      "retry": {
        "count": 2,
        "delayMs": 1000,
        "backoff": "exponential"
      },
      "timeout": "30s",
      "onFailure": "stop",
      "assertions": [
        { "field": "StatusCode", "operator": "Equal", "expected": "200" }
      ]
    },
    {
      "name": "getProfile",
      "type": "http",
      "input": {
        "url": "https://{{host}}/api/users/{{userId}}",
        "method": "Get",
        "headers": { "Authorization": ["Bearer {{token}}"] }
      },
      "assertions": [
        { "field": "StatusCode", "operator": "Equal", "expected": "200" }
      ]
    },
    {
      "name": "verifyDb",
      "type": "db",
      "input": {
        "dbType": "sqlserver",
        "connectionString": "{{dbConn}}",
        "sql": "SELECT 1 FROM Users WHERE Email = '{{email}}'",
        "commandType": "Scalar"
      },
      "assertions": [
        { "field": "Scalar", "operator": "Equal", "expected": "1" }
      ]
    }
  ],
  "parallel": [
    {
      "name": "checkBothServices",
      "steps": [
        {
          "name": "checkServiceA",
          "type": "http",
          "input": { "url": "https://{{host}}/service-a/health", "method": "Get" },
          "assertions": [{ "field": "StatusCode", "operator": "Equal", "expected": "200" }]
        },
        {
          "name": "checkServiceB",
          "type": "http",
          "input": { "url": "https://{{host}}/service-b/health", "method": "Get" },
          "assertions": [{ "field": "StatusCode", "operator": "Equal", "expected": "200" }]
        }
      ],
      "mode": "all",
      "timeout": "60s"
    }
  ]
}
```

### Step 结构

```
Step = {
  name:       步骤名称（日志/报错定位）
  type:       执行器类型（http/tcp/db/python）
  input:      执行参数（透传给 IStepExecutor）
  extract:    响应提取（可选）
  retry:      重试策略（可选）
  timeout:    步骤级超时（可选）
  onFailure:  失败行为（stop/skip/ignore，默认 stop）
  assertions: 步骤断言（可选，决定是否提前终止）
}
```

---

## 4. 新增类库：AutoTest.Dsl

### 职责

只做一件事：**将模板 JSON 解析为可执行的步骤序列（StepSequence）**

### 位置

```
src/AutoTest.Dsl/
├── AutoTest.Dsl.csproj
├── TemplateResolutionStep.cs     ← IPipelineStep 实现
├── IDslParser.cs                 ← DSL 解析器接口
├── DslParser.cs                  ← 默认实现（JsonDocument + 正则变量替换）
├── DslSchemaValidator.cs         ← Schema 校验（含 JSON Path 精确错误定位）
└── Models/
    ├── StepSequence.cs           ← 步骤序列定义（原 DagDefinition）
    ├── StepDefinition.cs         ← 单步定义
    ├── ParallelGroup.cs          ← 并行组定义
    ├── RetryPolicy.cs            ← 重试策略
    └── FailureStrategy.cs        ← 失败策略枚举
```

### 依赖

```
AutoTest.Dsl
  ├── AutoTest.Core (StepSequence / StepDefinition / IPipelineStep 等模型)
  
**不依赖** `AutoTest.Application`、`AutoTest.Infrastructure`、`AutoTest.Execution`、第三方包。

### TemplateResolutionStep

```csharp
public class TemplateResolutionStep : IPipelineStep
{
    private readonly IDslParser _parser;

    public async Task InvokeAsync(PipelineContext context, Func<Task> next)
    {
        if (!context.Monitor.IsTemplate)
        {
            await next();
            return;
        }

        var variables = JsonSerializer.Deserialize<Dictionary<string, string>>(
            context.Monitor.TemplateVariablesJson ?? "{}")!;

        var dag = await _parser.ParseAsync(
            context.Monitor.Target.ToJson(), variables);

        context.Items["DagDefinition"] = dag;
        context.Items["TemplateVariables"] = variables;

        await next();
    }
}
```

### IDslParser

```csharp
public interface IDslParser
{
    Task<DagDefinition> ParseAsync(string templateJson, Dictionary<string, string> variables);
}
```

**解析过程**：

```
JSON 字符串
   │
   ├── 1. Schema 校验
   │       ├── steps 是否为数组、每个 step 是否有 name/type/input
   │       ├── type 枚举是否合法（http/tcp/db/python）
   │       └── parallel 结构是否正确
   │       └── fail-fast：错误信息精确到 JSON 路径
   │
   ├── 2. 变量替换
   │       ├── 递归遍历 JSON 所有字符串值节点
   │       ├── 替换 {{var}} → 变量值
   │       ├── 支持默认值 {{var:default}}
   │       └── 缺失变量且无默认值 → 抛异常
   │
   └── 3. 构建 StepSequence
           ├── 串行步骤列表
           ├── 并行组列表
           └── 全局策略（超时、默认失败行为）
```

---

## 5. 新增类库：AutoTest.Orchestration

### 职责

**执行步骤序列**：上下文管理 → 步骤调度 → 重试/超时/降级 → 变量传播（含命名空间保护）→ 进度追踪

### 位置

```
src/AutoTest.Orchestration/
├── AutoTest.Orchestration.csproj
├── RuntimeOrchestrationStep.cs    ← IPipelineStep 实现
├── ExecutionContext.cs            ← 运行时上下文
├── ExecutionEngine.cs             ← 核心编排引擎
├── IStepExecutorResolver.cs       ← Executor 解析器接口
├── IProgressStore.cs              ← 进度持久化接口
└── Models/
    ├── StepExecutionRecord.cs     ← 步骤执行记录
    ├── TemplateDslResult.cs       ← DSL 最终结果
    └── StepAssertableResult.cs    ← 可断言的步骤结果
```

### 依赖

```
AutoTest.Orchestration
  ├── AutoTest.Core (模型 + IStepExecutor + IPipelineStep 接口)
  ├── AutoTest.Dsl (StepSequence 消费方)
  ├── common/CacheCommons (IDistributedLock)
  └── [Infrastructure 层的 IProgressStore 实现]

> ⚠ 注意：`IPipelineStep` 定义在 Core，Dsl 和 Orchestration 不直接依赖 Application，避免循环依赖。
```

### RuntimeOrchestrationStep

```csharp
public class RuntimeOrchestrationStep : IPipelineStep
{
    private readonly ExecutionEngine _engine;

    public async Task InvokeAsync(PipelineContext context, Func<Task> next)
    {
        if (!context.Monitor.IsTemplate)
        {
            await next();
            return;
        }

        var dag = context.Items["DagDefinition"] as DagDefinition;
        var variables = context.Items["TemplateVariables"] as Dictionary<string, string>;

        var execCtx = await _engine.ExecuteAsync(dag!, variables!);

        context.Items["ExecutionContext"] = execCtx;
        context.Result = BuildFinalResult(execCtx);

        await next();
    }
}
```

### 核心编排引擎

```csharp
public class ExecutionEngine
{
    private readonly IStepExecutorResolver _executorResolver;
    private readonly IResponseValueExtractor _extractor;
    private readonly IVariableResolver _variableResolver;
    private readonly IDistributedLock _distributedLock;
    private readonly IProgressStore _progressStore;

    public async Task<ExecutionContext> ExecuteAsync(StepSequence dag, Dictionary<string, string> initialVariables)
    {
        var ctx = new ExecutionContext { Variables = initialVariables, Dag = dag };

        // 分布式锁（使用 MonitorId 确保同一监控不并发执行）
        await using var lockHandle = await _distributedLock.AcquireAsync($"dsl-run-{ctx.Dag.Id}");
        if (lockHandle == null)
            throw new ConcurrentExecutionException("有其他实例正在执行");

        // 进度恢复
        await TryRestoreProgress(ctx);

        try
        {
            // 串行步骤
            for (int i = ctx.CurrentStepIndex; i < dag.Steps.Count; i++)
            {
                if (ctx.IsTerminated) break;
                await ExecuteSingleStep(ctx, dag.Steps[i]);
                await _progressStore.SaveAsync(ctx);
            }

            // 并行组
            foreach (var group in dag.ParallelGroups)
            {
                if (ctx.IsTerminated) break;
                await ExecuteParallelGroup(ctx, group);
                await _progressStore.SaveAsync(ctx);
            }
        }
        catch
        {
            ctx.IsTerminated = true;
            await _progressStore.SaveAsync(ctx);
            throw;
        }

        return ctx;
    }

    private async Task ExecuteSingleStep(ExecutionContext ctx, StepDefinition step)
    {
        // 1. 变量替换
        var resolvedInput = _variableResolver.ReplaceJson(step.Input, ctx.Variables);

        // 2. 重试循环
        var result = await RetryLoop(ctx, step, resolvedInput);

        if (result == null)
        {
            HandleStepFailure(ctx, step, "所有重试均失败");
            return;
        }

        // 3. 记录步骤
        ctx.CompletedSteps.Add(new StepExecutionRecord { ... });

        // 提取变量（自动添加 stepName. 前缀防冲突）
        if (step.Extract?.Count > 0 && result.IsSuccess)
        {
            var extracted = await _extractor.ExtractAsync(result.Body ?? "", result.Headers ?? [], step.Extract);
            foreach (var kv in extracted)
            {
                var prefixedKey = $"{step.Name}.{kv.Key}";
                if (ctx.Variables.ContainsKey(kv.Key) && !ctx.Variables.ContainsKey(prefixedKey))
                    throw new InvalidOperationException(
                        $"步骤 '{step.Name}' 变量 '{kv.Key}' 冲突，请使用 '{prefixedKey}' 引用");
                ctx.Variables[kv.Key] = kv.Value;
                ctx.Variables[prefixedKey] = kv.Value;
            }
        }

        // 5. 步骤断言
        if (step.Assertions?.Count > 0 && !EvaluateAssertions(result, step.Assertions))
        {
            ApplyFailureStrategy(ctx, step.OnFailure);
        }
    }
}
```

---

## 6. 新增类库规划：项目文件

### AutoTest.Dsl.csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <RootNamespace>AutoTest.Dsl</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\AutoTest.Core\AutoTest.Core.csproj" />
    <!-- 不直接引用 Application，IPipelineStep 来自 Core -->
  </ItemGroup>
</Project>
```

### AutoTest.Orchestration.csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <RootNamespace>AutoTest.Orchestration</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\AutoTest.Core\AutoTest.Core.csproj" />
    <ProjectReference Include="..\AutoTest.Dsl\AutoTest.Dsl.csproj" />
    <ProjectReference Include="..\common\CacheCommons\CacheCommons.csproj" />
  </ItemGroup>
</Project>
```

---

## 7. 分布式锁：统一放入 CacheCommons

### 现状

| 组件 | 位置 | 范围 |
|------|------|------|
| `ICacheService` | `common/CacheCommons` | public |
| `MemoryCacheService` | `common/CacheCommons` | public |
| `RedisService` | `AutoTest.Infrastructure` | **internal** |
| `RedisLock` | `AutoTest.Infrastructure` | public |

### 改后

```       

common/CacheCommons/
├── ICacheService.cs              ← 已有（防穿透/防击穿/防雪崩）
├── MemoryCacheService.cs         ← 已有
├── AddCacheServiceExtension.cs   ← 已有
├── IDistributedLock.cs           ← 新增：分布式锁抽象接口
├── RedisLock.cs                  ← 迁入：从 Infrastructure 迁来
├── RedisLockService.cs           ← 迁入：从 Infrastructure 迁来
└── DistributedLockExtensions.cs  ← 新增：DI 注册扩展
```

### IDistributedLock

```csharp
namespace CacheCommons;

/// <summary>
/// 分布式锁抽象
/// </summary>
public interface IDistributedLock
{
    /// <summary>
    /// 尝试获取锁
    /// </summary>
    Task<ILockHandle?> AcquireAsync(string key, TimeSpan? ttl = null);
}

public interface ILockHandle : IAsyncDisposable
{
    /// <summary>
    /// 续锁
    /// </summary>
    Task<bool> ExtendAsync(TimeSpan extra);

    /// <summary>
    /// 释放锁
    /// </summary>
    Task ReleaseAsync();
}
```

### RedisLock 迁入 CacheCommons

```csharp
namespace CacheCommons;

public class RedisLock : ILockHandle
{
    // 从 AutoTest.Infrastructure.RedisLock 迁入
    // 逻辑不变：SET NX + Lua 续锁/释放 + Timer 自动续锁
    // 依赖：StackExchange.Redis
}
```

### RedisLockService（简化 RedisService 并迁入）

```csharp
namespace CacheCommons;

public class RedisLockService
{
    private readonly ConnectionMultiplexer _redis;

    public RedisLockService(string connectionString)
    {
        _redis = ConnectionMultiplexer.Connect(connectionString);
    }

    public ILockHandle CreateLock(string key, TimeSpan timeout)
    {
        var db = _redis.GetDatabase();
        return new RedisLock(db, key, timeout);
    }

    /// <summary>
    /// 幂等标记（防重复执行）
    /// </summary>
    public async Task<bool> TrySetOnceAsync(string key, TimeSpan ttl)
    {
        var db = _redis.GetDatabase();
        return await db.StringSetAsync(key, "1", ttl, when: When.NotExists);
    }
}
```

### CacheCommons 的缓存防护（已有，维持不变）

```
缓存穿透：null 结果用短 TTL（1分钟）缓存，防重复查询
缓存击穿：ConcurrentDictionary<SemaphoreSlim> 防热点 key 并发重建
缓存雪崩：TTL + 随机偏移（0~30s），分散过期时间
```

### DI 注册扩展

```csharp
// AddCacheServiceExtension.cs 扩展
public static class AddCacheServiceExtension
{
    public static IServiceCollection AddCacheService(this IServiceCollection services)
    {
        services.AddMemoryCache();
        services.AddSingleton<ICacheService, MemoryCacheService>();
        return services;
    }

    public static IServiceCollection AddRedisLock(this IServiceCollection services,
        string connectionString)
    {
        services.AddSingleton(new RedisLockService(connectionString));
        services.AddSingleton<IDistributedLock>(sp =>
            sp.GetRequiredService<RedisLockService>());
        return services;
    }
}
```

### 可选项：LockCommons 独立类库

如果以后锁的种类增加（DB 锁、ZooKeeper 锁等），可以从 CacheCommons 拆出独立类库：

```
common/LockCommons/
├── LockCommons.csproj
├── IDistributedLock.cs
├── ILockHandle.cs
├── Redis/
│   ├── RedisLock.cs
│   └── RedisLockService.cs
├── Database/
│   ├── DatabaseLock.cs
│   └── DatabaseLockService.cs
└── LockServiceCollectionExtensions.cs
```

---

## 8. Executor 执行阶段

### 接口（Core 层）

```csharp
// 仍在 AutoTest.Core，不做类库拆分
public interface IStepExecutor
{
    string Type { get; }
    Task<StepResult> ExecuteAsync(JsonElement input, CancellationToken ct);
}

public class StepResult
{
    public int StatusCode { get; init; }
    public string? Body { get; init; }
    public Dictionary<string, string[]>? Headers { get; init; }
    public long ElapsedMs { get; init; }
    public bool IsSuccess { get; init; }
    public string? ErrorMessage { get; init; }
}
```

### 适配层（AutoTest.Execution）

```csharp
// HttpStepExecutor 包装 HttpExecutionEngine
public class HttpStepExecutor : IStepExecutor
{
    public string Type => "http";
    private readonly HttpExecutionEngine _engine;

    public async Task<StepResult> ExecuteAsync(JsonElement input, CancellationToken ct)
    {
        var target = JsonSerializer.Deserialize<HttpTarget>(input.GetRawText())!;
        var result = await _engine.ExecuteAsync(target);
        // 映射为 StepResult...
    }
}

// TcpStepExecutor、DbStepExecutor 同理
```

---

## 9. Assertion 断言阶段

### 两级断言

```
步骤断言（Step-level）— Runtime 编排内嵌
    ├── 作用：判断当前步骤是否成功
    ├── 结果：stop / skip / ignore
    └── 评估：ExecutionEngine

最终断言（Final-level）— Pipeline AssertionStep
    ├── 作用：判断整个监控任务是否通过
    ├── 结果：Pipeline 成败
    └── 评估：已有的 AssertionStep
```

### TemplateDslResult

```csharp
public class TemplateDslResult
{
    public List<StepAssertableResult> StepResults { get; init; } = new();
    public Dictionary<string, string> FinalVariables { get; init; } = new();
    public bool AllStepsPassed { get; init; }
}

public class StepAssertableResult
{
    public StepDefinition Step { get; init; }
    public StepResult Result { get; init; }
    public Dictionary<string, string> VariablesAfterStep { get; init; }
}
```

---

## 10. 容错机制

### 10.1 步骤级重试

```json
{
  "retry": {
    "count": 3,
    "delayMs": 1000,
    "backoff": "exponential",
    "retryableCodes": ["408", "429", "502", "503", "0"]
  }
}
```

| 配置 | 默认值 | 说明 |
|------|--------|------|
| `count` | 0 | 最大重试次数 |
| `delayMs` | 1000 | 初始延迟（ms） |
| `backoff` | fixed | fixed/exponential |
| `retryableCodes` | 空（全部） | 触发重试的状态码 |

指数退避：`delay = min(baseDelay * 2^(attempt-1), 30000)`

### 10.2 断路器

轻量化实现，无需独立类库，放在 `AutoTest.Orchestration` 内部：

```csharp
internal class CircuitBreaker
{
    private int _failureCount;
    private DateTime? _lastFailureAt;

    private const int Threshold = 5;
    private const int ResetMs = 60_000;

    public bool IsOpen()
    {
        if (_failureCount < Threshold) return false;
        if (_lastFailureAt == null) return false;
        if ((DateTime.UtcNow - _lastFailureAt.Value).TotalMilliseconds > ResetMs)
            return false;
        return true;
    }
}
```

### 10.3 超时传播

```
步骤级 timeout → 覆盖全局超时 → Runtime 编排超时 → Pipeline 超时
```

### 10.4 三级降级

| 策略 | 行为 | 场景 |
|------|------|------|
| `stop` | 失败立即终止 | 链式依赖（登录失败不查用户） |
| `skip` | 失败跳过，继续执行 | 可选检查项 |
| `ignore` | 视为成功，不影响后续 | 非关键信息采集 |

---

## 11. MonitorEntity 的变化

```csharp
public class MonitorEntity
{
    // ... 现有字段不变 ...

    /// <summary>
    /// 是否为 DSL 模板模式
    /// </summary>
    public bool IsTemplate { get; private set; }

    /// <summary>
    /// 模板变量 JSON
    /// </summary>
    public string? TemplateVariablesJson { get; private set; }
}
```

### 存储示例

| 字段 | 模板模式 | 普通模式 |
|------|---------|---------|
| `TargetConfig` | `{"steps":[{...}]}` | `{"url":"...","method":"Get"}` |
| `TemplateVariablesJson` | `{"host":"dc1.example.com"}` | `null` |
| `IsTemplate` | `true` | `false` |

**兼容性**：`IsTemplate=false` 的监控跳过后两个 Step，行为零变化。

---

## 12. Pipeline 集成总览

```
                           Pipeline
                              │
              ┌─────┬─────────┼─────────┬─────┐
              ▼     ▼         ▼         ▼     ▼
         Template  Runtime   Exec     Assertion
        Resolution  Orchest  Step      Step
          Step      Step     (跳过     (对 DSL
        (AutoTest. (AutoTest. DSL结果)  最终结果
         Dsl)      Orchest              断言)
                    ration)
```

### DI 注册

```csharp
// DslServiceCollectionExtensions.cs (AutoTest.Dsl)
services.AddScoped<IPipelineStep, TemplateResolutionStep>();

// OrchestrationServiceCollectionExtensions.cs (AutoTest.Orchestration)
services.AddScoped<IPipelineStep, RuntimeOrchestrationStep>();

// ApplicationServiceCollectionExtensions.cs (AutoTest.Application)
services.AddScoped<IPipelineStep, ExecutionStep>();    // 非模板模式执行
services.AddScoped<IPipelineStep, AssertionStep>();    // 断言（所有模式）
```

```csharp
// Program.cs 或 AddInfrastructureServiceCollectionExtensions.cs
services.AddCacheService();
services.AddRedisLock(configuration["Redis:ConnectionString"] ?? "localhost:6379");

services.AddScoped<IStepExecutor, HttpStepExecutor>();
services.AddScoped<IStepExecutor, TcpStepExecutor>();
services.AddScoped<IStepExecutor, DbStepExecutor>();
```

---

## 13. 不受影响的部分

| 模块 | 原因 |
|------|------|
| `AiWorker` / `AiAnalysisConsumer` | 失败事件仍走 Outbox → AI 分析 |
| `Outbox` / `Orchestrator` | Pipeline 容器，对步骤内部无感知 |
| `IExecutionEngine`（Http/Tcp/Db） | 被 IStepExecutor 包装调用，本身不变 |
| 所有断言实现 | 断言逻辑不变 |
| 现有普通监控 | `IsTemplate=false` 跳过 DSL 流程，零影响 |
| `ExecutionRecord` | 只记录最终结果 |

---

## 14. 实现步骤

| 步骤 | 内容 | 类库 | 依赖 | 涉及文件 |
|------|------|------|------|---------|
| 1 | `CacheCommons` 新增 `IDistributedLock` + `RedisLock` 迁入 | CacheCommons | 无 | `IDistributedLock.cs`, `RedisLock.cs`, `RedisLockService.cs`, `DistributedLockExtensions.cs` |
| 2 | `AutoTest.Core` 定义 `IStepExecutor` + `StepResult` + `StepSequence` + `IPipelineStep/IPipeline/PipelineContext` | Core | 无 | `IStepExecutor.cs`, `StepResult.cs`, `StepSequence.cs`, `IPipelineStep.cs`, `IPipeline.cs`, `PipelineContext.cs` |
| 3 | `AutoTest.Dsl` 新建项目 + `IDslParser` + `DslParser` + Schema 校验 | Dsl | Core+Application | `IDslParser.cs`, `DslParser.cs`, `DslSchemaValidator.cs`, `StepDefinition.cs`, `ParallelGroup.cs`, `RetryPolicy.cs`, `FailureStrategy.cs` |
| 4 | `AutoTest.Dsl` 实现 `TemplateResolutionStep`(IPipelineStep) | Dsl | 3 | `TemplateResolutionStep.cs` |
| 5 | `AutoTest.Orchestration` 新建项目 + `ExecutionContext` + `ExecutionEngine` | Orchestration | Core+Dsl+CacheCommons | `ExecutionContext.cs`, `ExecutionEngine.cs`, `IStepExecutorResolver.cs`, `IProgressStore.cs`, `StepExecutionRecord.cs`, `TemplateDslResult.cs`, `StepAssertableResult.cs` |
| 6 | `AutoTest.Orchestration` 实现 `RuntimeOrchestrationStep`(IPipelineStep) | Orchestration | 5 | `RuntimeOrchestrationStep.cs` |
| 7 | `AutoTest.Execution` 实现 `HttpStepExecutor` / `TcpStepExecutor` / `DbStepExecutor` | Execution | Core | `HttpStepExecutor.cs`, `TcpStepExecutor.cs`, `DbStepExecutor.cs` |
| 8 | `AutoTest.Core` 实现 `IVariableResolver` + `IResponseValueExtractor` | Core | 无 | `IVariableResolver.cs`, `IResponseValueExtractor.cs` |
| 9 | `MonitorEntity` 加 `IsTemplate` + `TemplateVariablesJson` | Core | 无 | `MonitorEntity.cs` (修改) |
| 10 | Pipeline DI 注册 + ExecutionStep 跳过逻辑 | Application | 3+6 | `ApplicationServiceCollectionExtensions.cs`, `ExecutionStep.cs` |
| 11 | FluentMigrator 迁移 | Migrations | 9 | `AddIsTemplateColumns.cs` |
| 12 | 批量创建 API (`/apply` / `/batch`) | Webapi | 10 | `MonitorController.cs` (新增方法) |
| 13 | 更新 http-usage.md 文档 | docs | 全步骤 | `http-usage.md` |
| | **DSL 模板架构额外文件变更** | | | |
| `DagDefinition.cs` → `StepSequence.cs` | 重命名 + 增加 `Id` 字段用于分布式锁 key | Core | 无 | `StepSequence.cs` |
| `DslSchemaValidator.cs` | 升级为 JSON Path 精确错误定位，校验 extract/assertions 嵌套字段 | Dsl | 3 | `DslSchemaValidator.cs` |
| `ExecutionEngine.cs` | 锁 key 改用 `dag.Id`(MonitorId)；变量提取自动添加 `stepName.` 前缀防冲突 | Orchestration | 5 | `ExecutionEngine.cs` |
| `RuntimeOrchestrationStep.cs` | 执行前设置 `dag.Id = monitor.Id` | Orchestration | 6 | `RuntimeOrchestrationStep.cs` |
| `DslServiceCollectionExtensions.cs` | 引用 `AutoTest.Core.ExecutionPipeline` 替换 Application | Dsl | 3 | `DslServiceCollectionExtensions.cs` |
 
```

## 9. FAQ

### Q1：为什么不用扁平命名，要用分层命名空间？

避免权限膨胀后命名冲突。例如 `menu.ai` 和 `api.ai.run` 在扁平命名下无法区分。分层后 `ui.menu.ai` 控制前端菜单，`api.ai.run` 控制后端 AI 接口。

### Q2：`data.*` 什么时候用？

当需要控制用户只能查看特定范围的数据时（如只看到自己创建的监控），使用 `data.*` 命名空间。当前未实现，仅预留。

### Q3：ui 权限不校验后端，安全吗？

安全。`ui.*` 仅控制前端展示，后端所有 API 都由 `api.*` 权限保护。用户即使绕过前端直接调用 API，也会被后端拦截。

### Q4：为什么叫 StepSequence 而不是 DAG？

原设计稿使用"DAG"（有向无环图），但当前的执行模型是"串行步骤 + 并行组"的平面结构，没有步骤间的依赖边（如 step3 依赖 step1 的输出）。为了避免给读者错误的预期，v4.1 改为 `StepSequence`。如果未来需要支持真正的 DAG 拓扑，只需在 `StepDefinition` 中增加 `dependsOn` 字段即可扩展。

### Q5：变量提取的命名空间如何工作？

每个步骤的 `extract` 产生的变量会自动添加 `stepName.` 前缀，同时保留简短名。例如：

```json
// step: "login" 提取 "token"
// 结果：
//   {{login.token}}  — 带命名空间
//   {{token}}        — 短名

// 如果下个步骤也提取同名变量 "token"，会抛出冲突异常
```

引用时建议使用带命名空间的完整路径（`{{login.token}}`）以避免歧义。

### Q6：分布式锁的 key 是什么？

锁 key = `dsl-run-{MonitorId}`，确保同一监控的模板执行不会并发。
如果分布式锁获取失败（有其他实例正在执行），`ExecutionEngine` 会抛出 `ConcurrentExecutionException`。

### Q7：JsonElement 类型变量支持吗？

当前 `IVariableResolver.ReplaceJson` 使用 `Dictionary<string, string>`，变量值只能是字符串。
如需传递数组或嵌套对象（如 `{{ipList}}` → `["10.0.0.1","10.0.0.2"]`），需要扩展为 `Dictionary<string, object>`。
此功能标记为 **Future**，当前通过 JSON 序列化变通。
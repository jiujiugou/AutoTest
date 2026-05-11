using System.Text.Json;
using AutoTest.Core.Dsl;
using AutoTest.Core.ExecutionPipeline;
using LockCommons;
using Microsoft.Extensions.Logging;

namespace AutoTest.Workflow;

/// <summary>
/// 工作流执行步骤：逐项调度 DAG，委托 Engine 执行 + Assertion 评估 + Extractor 提取。
/// 不自行实现执行、重试、断言、变量提取——全部委托给对应模块。
/// </summary>
public class WorkflowExecutionStep : IPipelineStep
{
    private readonly IStepExecutorResolver _executorResolver;
    private readonly IResponseValueExtractor _extractor;
    private readonly IVariableResolver _variableResolver;
    private readonly IDistributedLock _distributedLock;
    private readonly IProgressStore _progressStore;
    private readonly CircuitBreaker _circuitBreaker;
    private readonly IStepAssertionEvaluator _assertionEvaluator;
    private readonly ILogger<WorkflowExecutionStep> _logger;

    private static readonly string CtxKey = typeof(DslPipelineContext).FullName!;

    public WorkflowExecutionStep(
        IStepExecutorResolver executorResolver,
        IResponseValueExtractor extractor,
        IVariableResolver variableResolver,
        IDistributedLock distributedLock,
        IProgressStore progressStore,
        CircuitBreaker circuitBreaker,
        IStepAssertionEvaluator assertionEvaluator,
        ILogger<WorkflowExecutionStep> logger)
    {
        _executorResolver = executorResolver;
        _extractor = extractor;
        _variableResolver = variableResolver;
        _distributedLock = distributedLock;
        _progressStore = progressStore;
        _circuitBreaker = circuitBreaker;
        _assertionEvaluator = assertionEvaluator;
        _logger = logger;
    }

    public async Task InvokeAsync(PipelineContext context, Func<Task> next)
    {
        // 非 DSL 模板场景跳过，不阻塞管道
        if (context.Items[CtxKey] is not DslPipelineContext dslCtx)
        {
            await next();
            return;
        }

        var execCtx = await ExecuteDagAsync(dslCtx);

        var dslResult = new DslExecutionResult
        {
            Steps = execCtx.CompletedSteps,
            FinalVariables = execCtx.Variables,
            AllStepsPassed = !execCtx.IsTerminated
        };

        context.Items[typeof(DslExecutionResult).FullName!] = dslResult;

        // DSL 断言 → Core.Assertion.AssertionResult 转换，统一管线输出格式
        var stepAssertions = execCtx.CompletedSteps
            .Where(s => s.Assertions?.Count > 0)
            .SelectMany(s => s.Assertions!.Select(a => new Core.Assertion.AssertionResult(
                Guid.NewGuid(),
                $"{s.StepName}.{a.Field}",
                a.Passed,
                a.Actual,
                a.Expected,
                a.Passed ? null : $"{a.Field}: expected {a.Expected}, actual {a.Actual ?? "null"}"
            )))
            .ToList();

        var finalStep = execCtx.CompletedSteps.LastOrDefault();
        context.Result = new DslExecutionResultWrapper(
            finalStep?.IsSuccess ?? false,
            finalStep?.ErrorMessage)
        {
            Assertions = stepAssertions
        };

        await next();
    }

    /// <summary>
    /// 按 DAG Items 顺序执行所有步骤，每步完成后保存进度快照用于断点续跑。
    /// 分布式锁防止同一任务被多实例并发执行。
    /// </summary>
    private async Task<DslRuntimeContext> ExecuteDagAsync(DslPipelineContext dslCtx)
    {
        var ctx = new DslRuntimeContext
        {
            Variables = new Dictionary<string, string>(dslCtx.Variables),
            Dag = dslCtx.Dag
        };

        // 分布式锁：防止同一任务多实例并发执行
        await using var lockHandle = await _distributedLock.AcquireAsync($"dsl-run-{ctx.Dag.Id}");
        if (lockHandle == null)
            throw new InvalidOperationException($"有其他实例正在执行: {ctx.ExecutionId}");

        // 断点续跑：从上次中断位置恢复
        await TryRestoreProgress(ctx);

        try
        {
            for (int i = ctx.CurrentStepIndex; i < ctx.Dag.Items.Count; i++)
            {
                if (ctx.IsTerminated || ctx.CancellationToken.IsCancellationRequested)
                    break;

                ctx.CurrentStepIndex = i;

                switch (ctx.Dag.Items[i])
                {
                    case StepDefinition step:
                        await ExecuteSingleStep(ctx, step);
                        break;
                    case ParallelGroup group:
                        await ExecuteParallelGroup(ctx, group);
                        break;
                }

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

    /// <summary>
    /// 执行单个步骤的完整流程：断路器检查 → 变量解析 → 执行 → 记录 → 变量提取 → 断言评估。
    /// 每一步都委托给对应的接口实现，自身不包含具体逻辑。
    /// </summary>
    private async Task ExecuteSingleStep(DslRuntimeContext ctx, StepDefinition step)
    {
        var targetKey = $"{step.Type}:{JsonSerializer.Serialize(step.Input)}";

        // 断路器：同一目标连续失败达到阈值时直接跳过，避免雪崩
        if (_circuitBreaker.IsOpen(targetKey))
        {
            HandleStepFailure(ctx, step, "断路器已打开，跳过执行");
            return;
        }

        // 变量替换：将 {{varName}} 替换为运行时上下文中的实际值
        var resolvedInput = _variableResolver.ReplaceJson(step.Input.GetRawText(), ctx.Variables);

        // 按 type 查找执行器并执行
        var executor = _executorResolver.Resolve(step.Type);
        StepResult? result;
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ctx.CancellationToken);
            if (step.Timeout != null)
                cts.CancelAfter(step.Timeout.Value);

            result = await executor.ExecuteAsync(
                JsonDocument.Parse(resolvedInput).RootElement.Clone(), cts.Token);
            _circuitBreaker.RecordSuccess(targetKey);
        }
        catch (Exception ex)
        {
            _circuitBreaker.RecordFailure(targetKey);
            HandleStepFailure(ctx, step, $"执行失败: {ex.Message}");
            return;
        }

        // 记录步骤结果
        ctx.CompletedSteps.Add(new StepExecutionRecord
        {
            StepName = step.Name,
            Type = step.Type,
            IsSuccess = result.IsSuccess,
            StatusCode = result.StatusCode,
            ElapsedMs = result.ElapsedMs,
            Attempts = 1,
            ErrorMessage = result.ErrorMessage,
            Body = result.Body,
            Headers = result.Headers
        });

        // 变量提取：从响应中按 extract 规则提取值，写入运行时变量表
        if (step.Extract?.Count > 0 && result.IsSuccess)
        {
            var extracted = await _extractor.ExtractAsync(
                result.Body ?? "", result.Headers ?? [], step.Extract);
            foreach (var kv in extracted)
            {
                var prefixedKey = $"{step.Name}.{kv.Key}";
                // 防止不同步骤间变量名冲突：同名变量已有值时要求使用前缀引用
                if (ctx.Variables.ContainsKey(kv.Key) && !ctx.Variables.ContainsKey(prefixedKey))
                    throw new InvalidOperationException(
                        $"步骤 '{step.Name}' 提取的变量 '{kv.Key}' 与上一步骤变量名冲突，请使用 '{prefixedKey}' 引用");
                ctx.Variables[kv.Key] = kv.Value;
                ctx.Variables[prefixedKey] = kv.Value;
            }
        }

        // 断言评估：按 Assertions 规则逐条校验，不通过时应用 FailureStrategy
        if (step.Assertions?.Count > 0)
        {
            var assertionResults = _assertionEvaluator.Evaluate(result, step.Assertions);
            var record = ctx.CompletedSteps.Last();
            ctx.CompletedSteps[^1] = new StepExecutionRecord
            {
                StepName = record.StepName,
                Type = record.Type,
                IsSuccess = record.IsSuccess,
                StatusCode = record.StatusCode,
                ElapsedMs = record.ElapsedMs,
                Attempts = record.Attempts,
                ErrorMessage = record.ErrorMessage,
                Body = record.Body,
                Headers = record.Headers,
                Assertions = assertionResults,
                ExecutedAt = record.ExecutedAt
            };

            if (!assertionResults.TrueForAll(a => a.Passed))
                ApplyFailureStrategy(ctx, step.OnFailure);
        }
    }

    private async Task ExecuteParallelGroup(DslRuntimeContext ctx, ParallelGroup group)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ctx.CancellationToken);
        if (group.Timeout != null)
            cts.CancelAfter(group.Timeout.Value);

        var originalKeys = new HashSet<string>(ctx.Variables.Keys);

        var tasks = group.Steps.Select(step => ExecuteSingleStepInGroup(ctx, step, cts.Token, originalKeys));
        var results = await Task.WhenAll(tasks);

        var successCount = results.Count(r => r);
        if (group.Mode == ParallelMode.All && successCount < results.Length)
            ctx.IsTerminated = true;
    }

    /// <summary>
    /// 在并行组中执行单个步骤，使用独立的运行时上下文副本，避免变量污染。
    /// 步骤完成后将提取的变量合并回父上下文，检测并行变量冲突。
    /// </summary>
    private async Task<bool> ExecuteSingleStepInGroup(DslRuntimeContext ctx, StepDefinition step, CancellationToken ct, HashSet<string> originalKeys)
    {
        if (ctx.IsTerminated) return false;
        // 独立上下文副本，并行步骤间互不干扰
        var localCtx = new DslRuntimeContext
        {
            Variables = new Dictionary<string, string>(ctx.Variables),
            Dag = ctx.Dag,
            CancellationToken = ct
        };
        await ExecuteSingleStep(localCtx, step);
        // 合并回父上下文时检测并行变量冲突
        lock (ctx.Variables)
        {
            foreach (var kv in localCtx.Variables)
            {
                if (!originalKeys.Contains(kv.Key) && ctx.Variables.ContainsKey(kv.Key))
                    throw new InvalidOperationException(
                        $"并行步骤变量冲突: '{kv.Key}' 被多个并行步骤同时提取。请为不同并行步骤使用不同的 extract name。");
                ctx.Variables[kv.Key] = kv.Value;
            }
        }
        lock (ctx.CompletedSteps)
        {
            ctx.CompletedSteps.AddRange(localCtx.CompletedSteps);
        }
        return localCtx.CompletedSteps.LastOrDefault()?.IsSuccess ?? false;
    }

    /// <summary>
    /// 仅 Stop 策略会终止整个 DAG；Skip / Ignore 在执行阶段已由流程控制处理。
    /// </summary>
    private static void ApplyFailureStrategy(DslRuntimeContext ctx, FailureStrategy strategy)
    {
        if (strategy == FailureStrategy.Stop)
            ctx.IsTerminated = true;
    }

    /// <summary>
    /// 步骤执行异常（如断路器打开、执行器抛异常）的统一处理。
    /// </summary>
    private static void HandleStepFailure(DslRuntimeContext ctx, StepDefinition step, string reason)
    {
        ctx.CompletedSteps.Add(new StepExecutionRecord
        {
            StepName = step.Name,
            Type = step.Type,
            IsSuccess = false,
            ErrorMessage = reason
        });
        ApplyFailureStrategy(ctx, step.OnFailure);
    }

    /// <summary>
    /// 从持久化存储恢复执行进度，实现断点续跑。
    /// </summary>
    private async Task TryRestoreProgress(DslRuntimeContext ctx)
    {
        var saved = await _progressStore.RestoreAsync(ctx.ExecutionId);
        if (saved == null) return;

        ctx.CurrentStepIndex = saved.CurrentStepIndex;
        ctx.Variables = saved.Variables;
        ctx.CompletedSteps = saved.CompletedSteps;
        ctx.IsTerminated = saved.IsTerminated;
    }
}

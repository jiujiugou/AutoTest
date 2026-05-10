using System.Text.Json;
using AutoTest.Core.Dsl;
using LockCommons;

namespace AutoTest.Orchestration;

public class ExecutionEngine
{
    private readonly IStepExecutorResolver _executorResolver;
    private readonly IResponseValueExtractor _extractor;
    private readonly IVariableResolver _variableResolver;
    private readonly IDistributedLock _distributedLock;
    private readonly IProgressStore _progressStore;
    private readonly CircuitBreaker _circuitBreaker;

    public ExecutionEngine(
        IStepExecutorResolver executorResolver,
        IResponseValueExtractor extractor,
        IVariableResolver variableResolver,
        IDistributedLock distributedLock,
        IProgressStore progressStore,
        CircuitBreaker circuitBreaker)
    {
        _executorResolver = executorResolver;
        _extractor = extractor;
        _variableResolver = variableResolver;
        _distributedLock = distributedLock;
        _progressStore = progressStore;
        _circuitBreaker = circuitBreaker;
    }

    public async Task<DslRuntimeContext> ExecuteAsync(StepSequence dag, Dictionary<string, string> initialVariables)
    {
        var ctx = new DslRuntimeContext
        {
            Variables = new Dictionary<string, string>(initialVariables),
            Dag = dag
        };

        await using var lockHandle = await _distributedLock.AcquireAsync($"dsl-run-{ctx.Dag.Id}");
        if (lockHandle == null)
            throw new InvalidOperationException($"有其他实例正在执行: {ctx.ExecutionId}");

        await TryRestoreProgress(ctx);

        try
        {
            for (int i = ctx.CurrentStepIndex; i < dag.Items.Count; i++)
            {
                if (ctx.IsTerminated || ctx.CancellationToken.IsCancellationRequested)
                    break;

                ctx.CurrentStepIndex = i;

                switch (dag.Items[i])
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

    private async Task ExecuteSingleStep(DslRuntimeContext ctx, StepDefinition step)
    {
        var targetKey = $"{step.Type}:{JsonSerializer.Serialize(step.Input)}";
        if (_circuitBreaker.IsOpen(targetKey))
        {
            HandleStepFailure(ctx, step, "断路器已打开，跳过执行");
            return;
        }

        var resolvedInput = _variableResolver.ReplaceJson(step.Input.GetRawText(), ctx.Variables);

        var executor = _executorResolver.Resolve(step.Type);
        StepResult? result = null;
        var attempt = 0;
        var maxAttempts = (step.Retry?.Count ?? 0) + 1;

        while (attempt < maxAttempts && !ctx.CancellationToken.IsCancellationRequested)
        {
            attempt++;
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ctx.CancellationToken);
                if (step.Timeout != null)
                    cts.CancelAfter(step.Timeout.Value);

                result = await executor.ExecuteAsync(
                    JsonDocument.Parse(resolvedInput).RootElement.Clone(), cts.Token);
                _circuitBreaker.RecordSuccess(targetKey);
                break;
            }
            catch (OperationCanceledException) when (attempt < maxAttempts)
            {
                _circuitBreaker.RecordFailure(targetKey);
                await WaitBackoff(step.Retry, attempt);
            }
            catch (Exception ex) when (attempt < maxAttempts && IsRetryable(ex, step.Retry))
            {
                _circuitBreaker.RecordFailure(targetKey);
                await WaitBackoff(step.Retry, attempt);
            }
            catch
            {
                _circuitBreaker.RecordFailure(targetKey);
                throw;
            }
        }

        if (result == null)
        {
            HandleStepFailure(ctx, step, "所有重试均失败");
            return;
        }

        ctx.CompletedSteps.Add(new StepExecutionRecord
        {
            StepName = step.Name,
            Type = step.Type,
            IsSuccess = result.IsSuccess,
            StatusCode = result.StatusCode,
            ElapsedMs = result.ElapsedMs,
            Attempts = attempt,
            ErrorMessage = result.ErrorMessage,
            Body = result.Body,
            Headers = result.Headers
        });

        if (step.Extract?.Count > 0 && result.IsSuccess)
        {
            var extracted = await _extractor.ExtractAsync(
                result.Body ?? "", result.Headers ?? [], step.Extract);
            foreach (var kv in extracted)
            {
                var prefixedKey = $"{step.Name}.{kv.Key}";
                if (ctx.Variables.ContainsKey(kv.Key) && !ctx.Variables.ContainsKey(prefixedKey))
                    throw new InvalidOperationException(
                        $"步骤 '{step.Name}' 提取的变量 '{kv.Key}' 与上一步骤变量名冲突，请使用 '{prefixedKey}' 引用");
                ctx.Variables[kv.Key] = kv.Value;
                ctx.Variables[prefixedKey] = kv.Value;
            }
        }

        if (step.Assertions?.Count > 0)
        {
            var assertionResults = EvaluateAssertions(result, step.Assertions);
            var record = ctx.CompletedSteps.Last();
            // StepExecutionRecord 是 init-only，需要替换
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

    private async Task<bool> ExecuteSingleStepInGroup(DslRuntimeContext ctx, StepDefinition step, CancellationToken ct, HashSet<string> originalKeys)
    {
        if (ctx.IsTerminated) return false;
        var localCtx = new DslRuntimeContext
        {
            Variables = new Dictionary<string, string>(ctx.Variables),
            Dag = ctx.Dag,
            CancellationToken = ct
        };
        await ExecuteSingleStep(localCtx, step);
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

    private static List<StepAssertionResult> EvaluateAssertions(StepResult result, List<AssertionDef> assertions)
    {
        return assertions.Select(a =>
        {
            var actual = a.Field.ToLowerInvariant() switch
            {
                "statuscode" => result.StatusCode.ToString(),
                "body" => result.Body,
                "responsetime" => result.ElapsedMs.ToString(),
                "elapsed" => result.ElapsedMs.ToString(),
                "header" when a.HeaderKey != null && result.Headers != null
                    => result.Headers.TryGetValue(a.HeaderKey, out var vals) ? string.Join(",", vals) : null,
                _ => null
            };

            var passed = actual != null && a.Operator.ToLowerInvariant() switch
            {
                "equal" => string.Equals(actual, a.Expected, StringComparison.OrdinalIgnoreCase),
                "contains" => actual.Contains(a.Expected, StringComparison.OrdinalIgnoreCase),
                "notequals" => !string.Equals(actual, a.Expected, StringComparison.OrdinalIgnoreCase),
                "lessthan" => double.TryParse(actual, out var an) && double.TryParse(a.Expected, out var en) && an < en,
                "greaterthan" => double.TryParse(actual, out var an2) && double.TryParse(a.Expected, out var en2) && an2 > en2,
                _ => false
            };

            return new StepAssertionResult
            {
                Field = a.Field,
                Operator = a.Operator,
                Expected = a.Expected,
                Actual = actual,
                Passed = passed
            };
        }).ToList();
    }

    private static void ApplyFailureStrategy(DslRuntimeContext ctx, FailureStrategy strategy)
    {
        switch (strategy)
        {
            case FailureStrategy.Stop:
                ctx.IsTerminated = true;
                break;
            case FailureStrategy.Skip:
                break;
            case FailureStrategy.Ignore:
                break;
        }
    }

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

    private static async Task WaitBackoff(RetryPolicy? retry, int attempt)
    {
        var baseDelay = retry?.DelayMs ?? 1000;
        var delay = retry?.Backoff switch
        {
            BackoffMode.Exponential => Math.Min(baseDelay * (int)Math.Pow(2, attempt - 1), 30_000),
            _ => baseDelay
        };
        await Task.Delay(delay);
    }

    private static bool IsRetryable(Exception ex, RetryPolicy? retry)
    {
        return ex is OperationCanceledException or HttpRequestException;
    }

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

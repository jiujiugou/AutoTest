using AutoTest.Application.Execution;
using AutoTest.Core.Execution;
using AutoTest.Core.ExecutionPipeline;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Step;

/// <summary>
/// 执行步骤：选择合适的执行引擎并执行目标。
/// </summary>
public class ExecutionStep : IPipelineStep
{
    private readonly ExecutionEngineResolver _engineResolver;
    private readonly ILogger<ExecutionStep> _logger;

    public ExecutionStep(ExecutionEngineResolver engineResolver, ILogger<ExecutionStep> logger)
    {
        _engineResolver = engineResolver;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task InvokeAsync(PipelineContext context, Func<Task> next)
    {
        var engine = _engineResolver.Resolve(context.Monitor.Target);
        context.Result = await engine.ExecuteAsync(context.Monitor.Target);

        await next();
    }
}

using AutoTest.Application.Execution;
using AutoTest.Application.ExecutionPipeline;
using AutoTest.Core.Execution;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Step;

public class ExecutionStep : IPipelineStep
{
    private readonly ExecutionEngineResolver _engineResolver;
    private readonly ILogger<ExecutionStep> _logger;

    public ExecutionStep(ExecutionEngineResolver engineResolver, ILogger<ExecutionStep> logger)
    {
        _engineResolver = engineResolver;
        _logger = logger;
    }

    public async Task InvokeAsync(PipelineContext context, Func<Task> next)
    {
        try
        {
            var engine = _engineResolver.Resolve(context.Monitor.Target);
            context.Result = await engine.ExecuteAsync(context.Monitor.Target);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ExecutionStep failed");
            throw;
        }

        await next();
    }
}

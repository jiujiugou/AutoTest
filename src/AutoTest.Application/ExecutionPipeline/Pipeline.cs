using Microsoft.Extensions.Logging;

namespace AutoTest.Application.ExecutionPipeline;

/// <summary>
/// 默认执行管道实现：按注册顺序编排 <see cref="IPipelineStep"/> 并执行。
/// </summary>
public class Pipeline : IPipeline
{
    private readonly IEnumerable<IPipelineStep> _steps;
    private readonly ILogger<Pipeline> _logger;
    public Pipeline(IEnumerable<IPipelineStep> steps, ILogger<Pipeline> logger)
    {
        _steps = steps;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task ExecuteAsync(PipelineContext context)
    {
        Func<Task> pipeline = () => Task.CompletedTask;

        foreach (var step in _steps.Reverse())
        {
            var next = pipeline;
            pipeline = async () =>
            {
                try
                {
                    await step.InvokeAsync(context, next);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in pipeline step: {StepName}", step.GetType().Name);
                    throw;
                }
            };
        }

        await pipeline();
    }
}

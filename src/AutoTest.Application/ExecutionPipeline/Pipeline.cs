using Microsoft.Extensions.Logging;

namespace AutoTest.Application.ExecutionPipeline;

public class Pipeline : IPipeline
{
    private readonly IEnumerable<IPipelineStep> _steps;
    private readonly ILogger<Pipeline> _logger;
    public Pipeline(IEnumerable<IPipelineStep> steps, ILogger<Pipeline> logger)
    {
        _steps = steps;
        _logger = logger;
    }

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

using AutoTest.Logging;

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
                    _logger.LogInformation($"Starting pipeline step: {step.GetType().Name}");
                    await step.InvokeAsync(context, next);
                    _logger.LogInformation($"Finished pipeline step: {step.GetType().Name}");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error in pipeline step: {step.GetType().Name}, Exception: {ex.Message}", ex);
                    throw; // 继续抛出异常，交给上层处理
                }
            };
            await pipeline();
        }
    }
}

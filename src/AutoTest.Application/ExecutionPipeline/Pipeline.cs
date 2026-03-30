namespace AutoTest.Application.ExecutionPipeline;

public class Pipeline : IPipeline
{
    private readonly IEnumerable<IPipelineStep> _steps;

    public Pipeline(IEnumerable<IPipelineStep> steps)
    {
        _steps = steps;
    }

    public async Task ExecuteAsync(PipelineContext context)
    {
        Func<Task> pipeline = () => Task.CompletedTask;

        foreach (var step in _steps.Reverse())
        {
            var next = pipeline;
            pipeline = () => step.InvokeAsync(context, next);
        }

        await pipeline();
    }
}

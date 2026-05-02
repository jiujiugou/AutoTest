namespace AutoTest.Core.ExecutionPipeline;

public interface IPipelineStep
{
    Task InvokeAsync(PipelineContext context, Func<Task> next);
}

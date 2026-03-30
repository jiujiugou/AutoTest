namespace AutoTest.Application.ExecutionPipeline;

public interface IPipelineStep
{
    Task InvokeAsync(PipelineContext context, Func<Task> next);
}

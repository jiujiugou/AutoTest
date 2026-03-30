namespace AutoTest.Application.ExecutionPipeline;

public interface IPipeline
{
    Task ExecuteAsync(PipelineContext context);
}

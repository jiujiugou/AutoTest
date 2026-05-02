namespace AutoTest.Core.ExecutionPipeline;

public interface IPipeline
{
    Task ExecuteAsync(PipelineContext context);
}

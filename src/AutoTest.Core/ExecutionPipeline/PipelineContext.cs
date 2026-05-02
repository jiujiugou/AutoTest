namespace AutoTest.Core.ExecutionPipeline;

public class PipelineContext
{
    public MonitorEntity Monitor { get; set; }
    public ExecutionResult? Result { get; set; }
    public Exception? Exception { get; set; }
    public Dictionary<string, object> Items { get; set; } = new();

    public PipelineContext(MonitorEntity monitor)
    {
        Monitor = monitor;
    }
}

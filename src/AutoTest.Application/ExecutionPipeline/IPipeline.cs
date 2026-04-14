namespace AutoTest.Application.ExecutionPipeline;

/// <summary>
/// 执行管道：按步骤编排一次监控任务执行流程。
/// </summary>
public interface IPipeline
{
    /// <summary>
    /// 执行管道。
    /// </summary>
    /// <param name="context">本次执行上下文。</param>
    Task ExecuteAsync(PipelineContext context);
}

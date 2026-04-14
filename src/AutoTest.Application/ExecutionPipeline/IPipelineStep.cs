namespace AutoTest.Application.ExecutionPipeline;

/// <summary>
/// 管道步骤：实现一个可组合的执行环节（如执行、断言、记录等）。
/// </summary>
public interface IPipelineStep
{
    /// <summary>
    /// 执行当前步骤，并在完成后调用 <paramref name="next"/> 继续后续步骤。
    /// </summary>
    /// <param name="context">执行上下文。</param>
    /// <param name="next">调用后进入下一个步骤。</param>
    Task InvokeAsync(PipelineContext context, Func<Task> next);
}

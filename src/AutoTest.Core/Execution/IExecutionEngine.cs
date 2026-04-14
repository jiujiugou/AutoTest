namespace AutoTest.Core.Execution;

/// <summary>
/// 执行引擎：根据 <see cref="MonitorTarget"/> 的类型执行一次检查，并返回 <see cref="ExecutionResult"/>。
/// </summary>
/// <remarks>
/// 一个系统通常会注册多个执行引擎，并通过 <see cref="CanExecute"/> 在运行时选择合适的实现。
/// </remarks>
public interface IExecutionEngine
{
    /// <summary>
    /// 判断当前执行引擎是否支持执行指定目标。
    /// </summary>
    bool CanExecute(MonitorTarget target);

    /// <summary>
    /// 执行指定目标并返回执行结果。
    /// </summary>
    /// <param name="target">要执行的监控目标。</param>
    /// <returns>执行结果（包含执行成功与否、错误信息以及后续断言结果等）。</returns>
    Task<ExecutionResult> ExecuteAsync(MonitorTarget target);
}

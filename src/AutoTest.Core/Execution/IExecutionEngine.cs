namespace AutoTest.Core.Execution;

public interface IExecutionEngine
{
    bool CanExecute(MonitorTarget target);
    Task<ExecutionResult> ExecuteAsync(MonitorTarget target);
}

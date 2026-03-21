using System;

namespace AutoTest.Core.Execution;

public interface IExecutionOrchestrator
{
    //负责调度monitor执行流程
    ExecutionResult ExecuteMonitor(Monitor monitor);
    IEnumerable<ExecutionResult> ExecuteAllMonitors(IEnumerable<Monitor> monitors);
}

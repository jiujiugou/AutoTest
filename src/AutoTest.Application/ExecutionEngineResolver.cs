using System;
using AutoTest.Core;
using AutoTest.Core.Execution;

namespace AutoTest.Application.Execution;

public class ExecutionEngineResolver
{
    private readonly IEnumerable<IExecutionEngine> _engines;
    public ExecutionEngineResolver(IEnumerable<IExecutionEngine> engines)
    {
        _engines = engines;
    }
    public IExecutionEngine Resolve(MonitorTarget target)
    {
        var engine = _engines.FirstOrDefault(e => e.CanExecute(target));
        if (engine == null)
            throw new InvalidOperationException($"No execution engine found for target {target}");
        return engine;
    }
}

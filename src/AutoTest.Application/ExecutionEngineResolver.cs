using System;
using AutoTest.Core;
using AutoTest.Core.Execution;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Execution;

public class ExecutionEngineResolver
{
    private readonly IEnumerable<IExecutionEngine> _engines;
    private readonly ILogger<ExecutionEngineResolver> _logger;
    public ExecutionEngineResolver(IEnumerable<IExecutionEngine> engines, ILogger<ExecutionEngineResolver> logger)
    {
        _engines = engines;
        _logger = logger;
    }
    public IExecutionEngine Resolve(MonitorTarget target)
    {
        var engine = _engines.FirstOrDefault(e => e.CanExecute(target));
        if (engine == null)
        {
            _logger.LogError("No execution engine found for target type {TargetType}", target.Type);
            throw new InvalidOperationException($"No execution engine found for target {target.Type}");
        }
        return engine;
    }
}

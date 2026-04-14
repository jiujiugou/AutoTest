using System;
using AutoTest.Core;
using AutoTest.Core.Execution;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Execution;

/// <summary>
/// 执行引擎解析器：从已注册的 <see cref="IExecutionEngine"/> 集合中选择可执行指定目标的实现。
/// </summary>
public class ExecutionEngineResolver
{
    private readonly IEnumerable<IExecutionEngine> _engines;
    private readonly ILogger<ExecutionEngineResolver> _logger;
    public ExecutionEngineResolver(IEnumerable<IExecutionEngine> engines, ILogger<ExecutionEngineResolver> logger)
    {
        _engines = engines;
        _logger = logger;
    }
    /// <summary>
    /// 解析并返回可执行该目标的执行引擎。
    /// </summary>
    /// <param name="target">监控目标。</param>
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

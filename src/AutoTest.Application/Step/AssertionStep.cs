using AutoTest.Application.Builder;
using AutoTest.Application.ExecutionPipeline;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Step;

/// <summary>
/// 断言步骤：将 <see cref="AutoTest.Core.Assertion.AssertionRule"/> 映射为可执行断言，并对执行结果进行评估。
/// </summary>
public class AssertionStep : IPipelineStep
{
    private readonly IReadOnlyDictionary<string, IAssertionMap> _assertionMaps;
    private readonly AssertionEngine _assertionEngine;
    private readonly ILogger<AssertionStep> _logger;
    public AssertionStep(AssertionEngine assertionEngine, IEnumerable<IAssertionMap> assertionMaps, ILogger<AssertionStep> logger)
    {
        _assertionMaps = assertionMaps.ToDictionary(m => m.Type, StringComparer.OrdinalIgnoreCase);
        _assertionEngine = assertionEngine;
        _logger = logger;
    }
    /// <inheritdoc />
    public async Task InvokeAsync(PipelineContext context, Func<Task> next)
    {

        var assertions = context.Monitor.Assertions.Select(rule =>
        {
            if (!_assertionMaps.TryGetValue(rule.Type, out var mapper))
                throw new InvalidOperationException($"No assertion mapper found for type: {rule.Type}");
            return mapper.Map(rule);
        }).ToList();

        _logger.LogInformation("Assertion step started.");

        if (context.Result != null)
        {
            try
            {
                var results = await _assertionEngine.EvaluateAsync(context.Result, assertions);
                context.Result.Assertions = results;

                foreach (var r in results)
                {
                    if (r.IsSuccess)
                        _logger.LogInformation($"Assertion passed: {r.Target}, actual={r.Actual}, expected={r.Expected}");
                    else
                        _logger.LogError($"Assertion failed: {r.Target}, actual={r.Actual}, expected={r.Expected}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Assertion evaluation failed");
                throw;
            }
        }

        _logger.LogInformation("Assertion step finished.");

        await next();
    }

}

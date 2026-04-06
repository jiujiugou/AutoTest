using AutoTest.Application.Builder;
using AutoTest.Application.ExecutionPipeline;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Step;

public class AssertionStep : IPipelineStep
{
    private readonly IAssertionMap _assertionMap;
    private readonly AssertionEngine _assertionEngine;
    private readonly ILogger<AssertionStep> _logger;
    public AssertionStep(AssertionEngine assertionEngine, IAssertionMap assertionMap, ILogger<AssertionStep> logger)
    {
        _assertionMap = assertionMap;
        _assertionEngine = assertionEngine;
        _logger = logger;
    }
    public async Task InvokeAsync(PipelineContext context, Func<Task> next)
    {

        var assertions = context.Monitor.Assertions.Select(_assertionMap.Map).ToList();

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

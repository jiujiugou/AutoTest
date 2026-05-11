using AutoTest.Application.Step;
using AutoTest.Core.ExecutionPipeline;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Pipelines;

/// <summary>
/// 非模板管道：ExecutionStep → AssertionStep
/// </summary>
public class DefaultPipeline : IPipeline
{
    private readonly ExecutionStep _execution;
    private readonly AssertionStep _assertion;
    private readonly ILogger<DefaultPipeline> _logger;

    public DefaultPipeline(ExecutionStep execution, AssertionStep assertion, ILogger<DefaultPipeline> logger)
    {
        _execution = execution;
        _assertion = assertion;
        _logger = logger;
    }

    public async Task ExecuteAsync(PipelineContext context)
    {
        await _execution.InvokeAsync(context, async () =>
        {
            await _assertion.InvokeAsync(context, () => Task.CompletedTask);
        });
    }
}

using AutoTest.Core.ExecutionPipeline;
using AutoTest.Workflow;
using Microsoft.Extensions.Logging;

namespace AutoTest.Application.Pipelines;

/// <summary>
/// 模板管道：TemplateResolutionStep → WorkflowExecutionStep
/// </summary>
public class TemplatePipeline : IPipeline
{
    private readonly TemplateResolutionStep _template;
    private readonly WorkflowExecutionStep _workflow;
    private readonly ILogger<TemplatePipeline> _logger;

    public TemplatePipeline(
        TemplateResolutionStep template,
        WorkflowExecutionStep workflow,
        ILogger<TemplatePipeline> logger)
    {
        _template = template;
        _workflow = workflow;
        _logger = logger;
    }

    public async Task ExecuteAsync(PipelineContext context)
    {
        await _template.InvokeAsync(context, async () =>
        {
            await _workflow.InvokeAsync(context, () => Task.CompletedTask);
        });
    }
}

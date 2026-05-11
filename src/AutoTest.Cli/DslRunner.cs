using System.Text.Json;
using AutoTest.Core;
using AutoTest.Core.Dsl;
using AutoTest.Core.ExecutionPipeline;
using AutoTest.Core.Target;
using AutoTest.Core.Target.Template;
using AutoTest.Workflow;

namespace AutoTest.Cli;

/// <summary>
/// 直接执行 DSL JSON 文件，不依赖 DB 或 Hangfire。
/// 构建临时 MonitorEntity 走 TemplatePipeline，结果不进持久化。
/// </summary>
public class DslRunner
{
    private readonly IDslParser _parser;
    private readonly IPipeline _pipeline;

    public DslRunner(IDslParser parser, IPipeline pipeline)
    {
        _parser = parser;
        _pipeline = pipeline;
    }

    public async Task<DslRunResult> RunAsync(string filePath, Dictionary<string, string>? variables = null, int timeoutSeconds = 60)
    {
        var json = await File.ReadAllTextAsync(filePath);
        var root = JsonDocument.Parse(json).RootElement.Clone();

        DslSchemaValidator.Validate(root);

        variables ??= new Dictionary<string, string>();
        var dag = await _parser.ParseAsync(root.GetRawText(), variables);

        // 构造一个临时 MonitorEntity。Target.Type = "TEMPLATE" 确保 PipelineSelector 路由到 TemplatePipeline。
        var templateTarget = new TemplateTarget(root.GetRawText());
        var monitor = new MonitorEntity(
            Guid.NewGuid(),
            Path.GetFileNameWithoutExtension(filePath),
            templateTarget,
            MonitorStatus.Pending,
            null);
        monitor.SetTemplateVariables(JsonSerializer.Serialize(variables));

        var context = new PipelineContext(monitor);
        context.Items[typeof(DslPipelineContext).FullName!] = new DslPipelineContext
        {
            Dag = dag,
            Variables = variables
        };

        await _pipeline.ExecuteAsync(context);

        var result = context.Result;
        var dslResult = context.Items.TryGetValue(typeof(DslExecutionResult).FullName!, out var dslResultObj)
            ? dslResultObj as DslExecutionResult
            : null;

        return new DslRunResult
        {
            Success = result?.IsExecutionSuccess ?? false,
            ErrorMessage = result?.ErrorMessage,
            Assertions = result?.Assertions ?? new List<AutoTest.Core.Assertion.AssertionResult>(),
            DslExecutionResult = dslResult
        };
    }
}

public class DslRunResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public List<AutoTest.Core.Assertion.AssertionResult> Assertions { get; init; } = new();
    public DslExecutionResult? DslExecutionResult { get; init; }
}

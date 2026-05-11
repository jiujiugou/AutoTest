using System.Text.Json.Serialization;

namespace AutoTest.Core.Dsl;

/// <summary>
/// DSL 管道上下文：承载模板解析后的 DAG、初始变量和执行结果。
/// 由 <see cref="TemplateResolutionStep"/> 写入 PipelineContext.Items，供 <see cref="WorkflowExecutionStep"/> 读取。
/// </summary>
public class DslPipelineContext
{
    public StepSequence Dag { get; set; } = null!;
    public Dictionary<string, string> Variables { get; set; } = new();
    public DslRuntimeContext? Result { get; set; }
}

/// <summary>
/// DSL 运行时的可变上下文：变量快照、已完成步骤、当前进度。
/// 每次 SaveAsync 时持久化，用于断点续跑。
/// </summary>
public class DslRuntimeContext
{
    public string ExecutionId { get; init; } = Guid.NewGuid().ToString("N");
    /// <summary>运行时变量：初始值 + 各步骤 extract 的累积结果。</summary>
    public Dictionary<string, string> Variables { get; set; } = new();
    public List<StepExecutionRecord> CompletedSteps { get; set; } = new();
    /// <summary>当前执行到的步骤索引（0-based），用于断点续跑定位。</summary>
    public int CurrentStepIndex { get; set; }
    public bool IsTerminated { get; set; }
    public StepSequence Dag { get; init; } = null!;
    [JsonIgnore]
    public CancellationToken CancellationToken { get; set; }
}

public class StepExecutionRecord
{
    public string StepName { get; init; } = "";
    public string Type { get; init; } = "";
    public bool IsSuccess { get; init; }
    public int StatusCode { get; init; }
    public long ElapsedMs { get; init; }
    public int Attempts { get; init; }
    public string? ErrorMessage { get; init; }
    public string? Body { get; init; }
    public Dictionary<string, string[]>? Headers { get; init; }
    public List<StepAssertionResult>? Assertions { get; init; }
    public DateTime ExecutedAt { get; init; } = DateTime.UtcNow;
}

public class StepAssertionResult
{
    public string Field { get; init; } = "";
    public string Operator { get; init; } = "";
    public string Expected { get; init; } = "";
    public string? Actual { get; init; }
    public bool Passed { get; init; }
}

/// <summary>
/// DSL 执行最终结果汇总。
/// </summary>
public class DslExecutionResult
{
    public List<StepExecutionRecord> Steps { get; init; } = new();
    public Dictionary<string, string> FinalVariables { get; init; } = new();
    public bool AllStepsPassed { get; init; }
}

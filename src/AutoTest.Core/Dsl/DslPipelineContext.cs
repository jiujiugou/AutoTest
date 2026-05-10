using System.Text.Json.Serialization;

namespace AutoTest.Core.Dsl;

public class DslPipelineContext
{
    public StepSequence Dag { get; set; } = null!;
    public Dictionary<string, string> Variables { get; set; } = new();
    public DslRuntimeContext? Result { get; set; }
}

public class DslRuntimeContext
{
    public string ExecutionId { get; init; } = Guid.NewGuid().ToString("N");
    public Dictionary<string, string> Variables { get; set; } = new();
    public List<StepExecutionRecord> CompletedSteps { get; set; } = new();
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

public class DslExecutionResult
{
    public List<StepExecutionRecord> Steps { get; init; } = new();
    public Dictionary<string, string> FinalVariables { get; init; } = new();
    public bool AllStepsPassed { get; init; }
}

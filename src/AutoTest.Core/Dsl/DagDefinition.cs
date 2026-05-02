using System.Text.Json;

namespace AutoTest.Core.Dsl;

public class StepSequence
{
    public string Id { get; set; } = string.Empty;
    public List<StepDefinition> Steps { get; set; } = new();
    public List<ParallelGroup> ParallelGroups { get; set; } = new();
    public TimeSpan? GlobalTimeout { get; set; }
    public FailureStrategy DefaultFailureStrategy { get; set; } = FailureStrategy.Stop;
}

public class StepDefinition
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public JsonElement Input { get; set; }
    public List<ValueExtractor>? Extract { get; set; }
    public RetryPolicy? Retry { get; set; }
    public TimeSpan? Timeout { get; set; }
    public FailureStrategy OnFailure { get; set; } = FailureStrategy.Stop;
    public List<AssertionDef>? Assertions { get; set; }
}

public class ParallelGroup
{
    public string Name { get; set; } = "";
    public List<StepDefinition> Steps { get; set; } = new();
    public ParallelMode Mode { get; set; } = ParallelMode.All;
    public TimeSpan? Timeout { get; set; }
}

public class RetryPolicy
{
    public int Count { get; set; } = 0;
    public int DelayMs { get; set; } = 1000;
    public BackoffMode Backoff { get; set; } = BackoffMode.Fixed;
    public List<string>? RetryableCodes { get; set; }
}

public class ValueExtractor
{
    public string Name { get; set; } = "";
    public ExtractSource Source { get; set; }
    public ExtractMethod Method { get; set; }
    public string Expression { get; set; } = "";
}

public class AssertionDef
{
    public string Field { get; set; } = "";
    public string Operator { get; set; } = "";
    public string Expected { get; set; } = "";
    public string? HeaderKey { get; set; }
}

public enum ExtractSource { Body, Header }
public enum ExtractMethod { JsonPath, Regex, Plain }
public enum BackoffMode { Fixed, Exponential }
public enum FailureStrategy { Stop, Skip, Ignore }
public enum ParallelMode { All, Any }

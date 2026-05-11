using System.Text.Json;

namespace AutoTest.Core.Dsl;

/// <summary>
/// DSL 模板解析后的有向无环图（DAG）定义。
/// Items 按顺序包含 Steps 和 ParallelGroups，决定执行顺序。
/// </summary>
public class StepSequence
{
    public string Id { get; set; } = string.Empty;
    /// <summary>按 DSL 声明顺序排列的执行项（先 steps，后 parallel groups）。</summary>
    public List<SequenceItem> Items { get; set; } = new();
    /// <summary>解析后的顺序步骤。</summary>
    public List<StepDefinition> Steps { get; set; } = new();
    /// <summary>解析后的并行组。</summary>
    public List<ParallelGroup> ParallelGroups { get; set; } = new();
    public TimeSpan? GlobalTimeout { get; set; }
    public FailureStrategy DefaultFailureStrategy { get; set; } = FailureStrategy.Stop;
}

public abstract class SequenceItem { }

/// <summary>
/// 单个 DSL 步骤定义 —— 对应 DSL JSON 中 steps[] 的一条。
/// </summary>
public class StepDefinition : SequenceItem
{
    public string Name { get; set; } = "";
    /// <summary>步骤类型：http / tcp / db / python。</summary>
    public string Type { get; set; } = "";
    /// <summary>步骤输入参数，由 <see cref="IVariableResolver"/> 处理后传入执行器。</summary>
    public JsonElement Input { get; set; }
    /// <summary>执行后从响应中提取变量到上下文的规则列表。</summary>
    public List<ValueExtractor>? Extract { get; set; }
    public RetryPolicy? Retry { get; set; }
    public TimeSpan? Timeout { get; set; }
    /// <summary>失败时的处理策略，默认 Stop。</summary>
    public FailureStrategy OnFailure { get; set; } = FailureStrategy.Stop;
    public List<AssertionDef>? Assertions { get; set; }
}

/// <summary>
/// 并行步骤组 —— 对应 DSL JSON 中 parallel[] 的一条。
/// 组内所有步骤并发执行。
/// </summary>
public class ParallelGroup : SequenceItem
{
    public string Name { get; set; } = "";
    public List<StepDefinition> Steps { get; set; } = new();
    /// <summary>All: 全部成功才通过；Any: 任一成功即通过。</summary>
    public ParallelMode Mode { get; set; } = ParallelMode.All;
    public TimeSpan? Timeout { get; set; }
}

public class RetryPolicy
{
    public int Count { get; set; } = 0;
    public int DelayMs { get; set; } = 1000;
    public BackoffMode Backoff { get; set; } = BackoffMode.Fixed;
    /// <summary>可重试的 HTTP 状态码列表；null 表示所有失败都重试。</summary>
    public List<string>? RetryableCodes { get; set; }
}

/// <summary>
/// 变量提取规则：从响应 Body 或 Header 中用指定方法提取值，存入变量上下文。
/// </summary>
public class ValueExtractor
{
    /// <summary>变量名，后续步骤通过 <c>{{name}}</c> 引用。</summary>
    public string Name { get; set; } = "";
    public ExtractSource Source { get; set; }
    public ExtractMethod Method { get; set; }
    /// <summary>提取表达式（JsonPath / Regex / Header Key）。</summary>
    public string Expression { get; set; } = "";
}

/// <summary>
/// 断言规则定义 —— 对应 DSL JSON 中 assertions[] 的一条。
/// </summary>
public class AssertionDef
{
    /// <summary>断言字段名：StatusCode / Body / ResponseTime / Header。</summary>
    public string Field { get; set; } = "";
    /// <summary>比较操作符：Equal / Contains / NotEquals / LessThan / GreaterThan。</summary>
    public string Operator { get; set; } = "";
    public string Expected { get; set; } = "";
    /// <summary>Field 为 Header 时，指定具体 Header 的 key。</summary>
    public string? HeaderKey { get; set; }
}

public enum ExtractSource { Body, Header }
public enum ExtractMethod { JsonPath, Regex, Plain }
public enum BackoffMode { Fixed, Exponential }
/// <summary>Stop: 终止整个 DAG；Skip: 跳过当前步骤继续下一个；Ignore: 忽略失败继续。</summary>
public enum FailureStrategy { Stop, Skip, Ignore }
/// <summary>All: 全部成功；Any: 任一成功。</summary>
public enum ParallelMode { All, Any }

using System.Text.Json;

namespace AutoTest.Core.Dsl;

/// <summary>
/// DSL 步骤执行器 —— 每种 step type（http/tcp/db/python）对应一个实现。
/// 由 <see cref="IStepExecutorResolver"/> 按 type 查找。
/// </summary>
public interface IStepExecutor
{
    /// <summary>
    /// 步骤类型标识，不区分大小写，如 "http"、"tcp"、"db"、"python"。
    /// </summary>
    string Type { get; }

    /// <summary>
    /// 执行单个 DSL 步骤。
    /// </summary>
    /// <param name="input">已解析变量后的步骤 input JSON（由 <see cref="IVariableResolver"/> 预处理）。</param>
    Task<StepResult> ExecuteAsync(JsonElement input, CancellationToken ct = default);
}

/// <summary>
/// 单步执行结果，与具体执行器类型无关的统一模型。
/// </summary>
public class StepResult
{
    public int StatusCode { get; init; }
    public string? Body { get; init; }
    public Dictionary<string, string[]>? Headers { get; init; }
    public long ElapsedMs { get; init; }
    public bool IsSuccess { get; init; }
    public string? ErrorMessage { get; init; }
}

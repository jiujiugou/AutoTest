using AutoTest.Core;
using AutoTest.Core.Assertion;

namespace AutoTest.Core.Dsl;

/// <summary>
/// 将 DSL 执行结果适配为管线通用的 <see cref="ExecutionResult"/>，包含 DSL 断言转换结果。
/// </summary>
public class DslExecutionResultWrapper : ExecutionResult
{
    public DslExecutionResultWrapper(bool success, string? errorMessage)
        : base(success, errorMessage ?? "")
    {
    }
}

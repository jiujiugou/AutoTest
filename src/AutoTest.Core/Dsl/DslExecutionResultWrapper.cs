using AutoTest.Core;
using AutoTest.Core.Assertion;

namespace AutoTest.Core.Dsl;

public class DslExecutionResultWrapper : ExecutionResult
{
    public DslExecutionResultWrapper(bool success, string? errorMessage)
        : base(success, errorMessage ?? "")
    {
    }
}

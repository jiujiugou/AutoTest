using AutoTest.Core;

namespace AutoTest.Assertions.Http;

public interface IField
{
    bool CanResolve(ExecutionResult context);

    object? Resolve(HttpAssertionField field, ExecutionResult context, string? key = null);
}

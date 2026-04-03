using AutoTest.Core.Execution;

namespace AutoTest.Assertions.Http;

public interface IResolver
{
    bool CanResolve(string field);
    object? Resolve(string field, IHttpExecutionResult result);
}

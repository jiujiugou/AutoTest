using AutoTest.Core.Execution;

namespace AutoTest.Assertions.Http.Resolver;

public class BodyResolver : IResolver
{
    public bool CanResolve(string field) => field == "body";

    public object? Resolve(string field, IHttpExecutionResult result)
        => result.Body;
}
using AutoTest.Core.Execution;

namespace AutoTest.Assertions.Http.Resolver;

public class ElapsedResolver : IResolver
{
    public bool CanResolve(string field) => field == "elapsed";

    public object? Resolve(string field, IHttpExecutionResult result)
        => result.ElapsedMilliseconds;
}

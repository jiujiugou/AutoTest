using AutoTest.Core.Execution;

namespace AutoTest.Assertions.Http.Resolver;

public class StatusCodeResolver : IResolver
{
    public bool CanResolve(string field)
    {
        return field == "statusCode";
    }

    public object? Resolve(string field, IHttpExecutionResult result)
    {
        return result.StatusCode;
    }

}

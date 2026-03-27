namespace AutoTest.Core.Execution;

public interface IHttpExecutionResult
{
    int StatusCode { get; }
    string Body { get; }
}

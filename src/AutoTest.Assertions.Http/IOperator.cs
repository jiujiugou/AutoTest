namespace AutoTest.Assertions.Http.Operator;

public interface IOperator
{
    bool CanHandle(HttpAssertionOperator op);
    bool Evaluate(object? actual, string expected);
}

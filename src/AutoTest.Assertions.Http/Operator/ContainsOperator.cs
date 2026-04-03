namespace AutoTest.Assertions.Http.Operator;

public class ContainsOperator : IOperator
{
    public bool CanHandle(HttpAssertionOperator op)
        => op == HttpAssertionOperator.Contains;

    public bool Evaluate(object? actual, string expected)
        => actual?.ToString()?.Contains(expected) == true;
}

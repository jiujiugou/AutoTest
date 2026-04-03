namespace AutoTest.Assertions.Http.Operator;

public class EqualOperator : IOperator
{
    public bool CanHandle(HttpAssertionOperator op)
        => op == HttpAssertionOperator.Equal;

    public bool Evaluate(object? actual, string expected)
        => actual?.ToString() == expected;
}

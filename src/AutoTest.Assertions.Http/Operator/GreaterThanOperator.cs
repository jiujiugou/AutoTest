namespace AutoTest.Assertions.Http.Operator;

public class GreaterThanOperator : IOperator
{
    public bool CanHandle(HttpAssertionOperator op)
        => op == HttpAssertionOperator.GreaterThan;

    public bool Evaluate(object? actual, string expected)
    {
        if (double.TryParse(actual?.ToString(), out var a) &&
            double.TryParse(expected, out var e))
        {
            return a > e;
        }

        return false;
    }
}

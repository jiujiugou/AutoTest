using AutoTest.Assertions;

namespace AutoTest.Assertion
{
    internal class DefaultOperator : IOperator
    {
        public AssertionOperator Operator { get; set; }

        public DefaultOperator(AssertionOperator assertionOperator)
        {
            Operator = assertionOperator;
        }

        public bool Evaluate(object? actual, object? expected)
        {
            return Operator switch
            {
                AssertionOperator.Equal => actual?.ToString() == expected?.ToString(),
                AssertionOperator.NotEqual => actual?.ToString() != expected?.ToString(),
                AssertionOperator.Contains => actual?.ToString()?.Contains(expected?.ToString() ?? "") == true,
                AssertionOperator.LessThan => Compare(actual, expected) < 0,
                AssertionOperator.LessThanOrEqual => Compare(actual, expected) <= 0,
                AssertionOperator.GreaterThan => Compare(actual, expected) > 0,
                AssertionOperator.GreaterThanOrEqual => Compare(actual, expected) >= 0,
                _ => throw new NotSupportedException($"Unsupported operator: {Operator}")
            };
        }

        private int Compare(object? a, object? b)
        {
            if (a == null || b == null)
                throw new InvalidOperationException("Cannot compare null values");

            if (a is IComparable ca)
            {
                var convertedB = Convert.ChangeType(b, a.GetType());
                return ca.CompareTo(convertedB);
            }

            throw new InvalidOperationException($"Type {a.GetType()} is not comparable");
        }
    }
}

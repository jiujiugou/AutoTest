using AutoTest.Core;
using AutoTest.Core.Assertion;
using AutoTest.Core.Execution;

namespace AutoTest.Assertion.Python
{
    public sealed class PythonAssertion : IAssertion
    {
        public Guid Id { get; }
        private readonly string _field;
        private readonly AssertionOperator _operator;
        private readonly string _expected;

        public PythonAssertion(Guid id, string field, string op, string expected)
        {
            Id = id;
            _field = field ?? string.Empty;
            _expected = expected ?? string.Empty;
            _operator = Enum.TryParse<AssertionOperator>(op, true, out var parsed) ? parsed : AssertionOperator.Equal;
        }

        public Task<AssertionResult> EvaluateAsync(ExecutionResult executionResult)
        {
            if (executionResult is not IPythonExecutionResult py)
            {
                return Task.FromResult(new AssertionResult(
                    Id,
                    _field,
                    false,
                    null,
                    _expected,
                    "Execution result is not PythonExecutionResult"
                ));
            }

            var (ok, actual) = Resolve(py, _field);
            if (!ok)
            {
                return Task.FromResult(new AssertionResult(
                    Id,
                    _field,
                    false,
                    null,
                    _expected,
                    $"Unsupported field: {_field}"
                ));
            }

            var success = Evaluate(actual, _expected, _operator);
            return Task.FromResult(new AssertionResult(
                Id,
                _field,
                success,
                actual?.ToString(),
                _expected,
                success ? "OK" : $"Assertion failed: actual={actual}, expected={_expected}, operator={_operator}"
            ));
        }

        private static (bool Ok, object? Value) Resolve(IPythonExecutionResult py, string field)
        {
            var f = (field ?? string.Empty).Trim();
            return f switch
            {
                "ExitCode" => (true, py.ExitCode),
                "StdOut" => (true, py.StdOut ?? string.Empty),
                "StdErr" => (true, py.StdErr ?? string.Empty),
                "ElapsedMs" => (true, py.ElapsedMs),
                "TimedOut" => (true, py.TimedOut),
                _ => (false, null)
            };
        }

        private static bool Evaluate(object? actual, string expected, AssertionOperator op)
        {
            if (actual == null)
            {
                return op switch
                {
                    AssertionOperator.Equal => string.IsNullOrEmpty(expected),
                    AssertionOperator.NotEqual => !string.IsNullOrEmpty(expected),
                    _ => false
                };
            }

            if (actual is bool ab)
            {
                if (!bool.TryParse(expected, out var eb))
                    return false;
                return op switch
                {
                    AssertionOperator.Equal => ab == eb,
                    AssertionOperator.NotEqual => ab != eb,
                    _ => false
                };
            }

            if (actual is int ai)
            {
                if (!long.TryParse(expected, out var ei))
                    return false;
                return Compare(ai, ei, op);
            }

            if (actual is long al)
            {
                if (!long.TryParse(expected, out var el))
                    return false;
                return Compare(al, el, op);
            }

            var a = actual.ToString() ?? string.Empty;
            return op switch
            {
                AssertionOperator.Equal => string.Equals(a, expected, StringComparison.Ordinal),
                AssertionOperator.NotEqual => !string.Equals(a, expected, StringComparison.Ordinal),
                AssertionOperator.Contains => a.Contains(expected, StringComparison.Ordinal),
                _ => false
            };
        }

        private static bool Compare(long a, long b, AssertionOperator op)
        {
            return op switch
            {
                AssertionOperator.Equal => a == b,
                AssertionOperator.NotEqual => a != b,
                AssertionOperator.LessThan => a < b,
                AssertionOperator.LessThanOrEqual => a <= b,
                AssertionOperator.GreaterThan => a > b,
                AssertionOperator.GreaterThanOrEqual => a >= b,
                _ => false
            };
        }
    }

}

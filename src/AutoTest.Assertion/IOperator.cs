using AutoTest.Assertion;

namespace AutoTest.Assertions;

public interface IOperator
{
    AssertionOperator Operator { get; set; }
    bool Evaluate(object? actual, object? expected);
}

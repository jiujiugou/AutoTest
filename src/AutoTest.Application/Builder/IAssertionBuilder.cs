using AutoTest.Core.Assertion;

namespace AutoTest.Application;

public interface IAssertionBuilder
{
    string Type { get; }
    IAssertion Build(AssertionRule rule);
}

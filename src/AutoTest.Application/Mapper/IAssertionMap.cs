using AutoTest.Core.Assertion;

namespace AutoTest.Application;

public interface IAssertionMap
{
    string Type { get; }
    IAssertion Map(AssertionRule rule);
}

using AutoTest.Core.Assertion;

namespace AutoTest.Application;

public interface IAssertionMap
{
    IAssertion Map(AssertionRule rule);
}

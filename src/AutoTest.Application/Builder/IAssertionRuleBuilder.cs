using AutoTest.Core.Assertion;

namespace AutoTest.Application.Builder;

public interface IAssertionRuleBuilder
{
    string Type { get; }
    AssertionRule Build(string json);
}

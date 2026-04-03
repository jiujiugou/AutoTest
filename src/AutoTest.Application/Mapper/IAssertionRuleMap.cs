using AutoTest.Core.Assertion;

namespace AutoTest.Application.Builder;

public interface IAssertionRuleMap
{
    string Type { get; }
    AssertionRule Map(Guid id, string json);
}

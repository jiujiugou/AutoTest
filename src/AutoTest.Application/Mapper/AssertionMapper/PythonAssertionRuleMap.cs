using AutoTest.Application.Builder;
using AutoTest.Core.Assertion;

namespace AutoTest.Application.Mapper.AssertionMapper;

public sealed class PythonAssertionRuleMap : IAssertionRuleMap
{
    public string Type => "PYTHON";

    public AssertionRule Map(Guid id, string json)
    {
        return new AssertionRule(id, Type, json);
    }
}

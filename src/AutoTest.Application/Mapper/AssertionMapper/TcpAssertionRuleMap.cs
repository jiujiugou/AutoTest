using AutoTest.Application.Builder;
using AutoTest.Core.Assertion;

namespace AutoTest.Application.Mapper.AssertionMapper;

public sealed class TcpAssertionRuleMap : IAssertionRuleMap
{
    public string Type => "TCP";

    public AssertionRule Map(Guid id, string json)
    {
        return new AssertionRule(id, Type, json);
    }
}

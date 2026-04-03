using System.Text.Json;
using AutoTest.Application.Dto;
using AutoTest.Core.Assertion;

namespace AutoTest.Application.Builder.AssertionBuilder;

public class AssertionRuleMap : IAssertionRuleMap
{
    public string Type => "HTTP";

    public AssertionRule Map(Guid id, string json)
    {
        return new AssertionRule(
        id,
        Type,
        json
    );
    }

}

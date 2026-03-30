using System.Text.Json;
using AutoTest.Application.Dto;
using AutoTest.Assertions.Http;
using AutoTest.Core.Assertion;

namespace AutoTest.Application.Builder.AssertionBuilder;

public class HttpAssertionBuilder : IAssertionBuilder
{
    public string Type => "HTTP";

    public IAssertion Build(AssertionRule rule)
    {
        var dto = JsonSerializer.Deserialize<HttpAssertionDto>(rule.ConfigJson)!;

        return new HttpAssertion(
            dto.Id,
            dto.Field,
            dto.Operator,
            dto.Expected
        );
    }
}

using System.Text.Json;
using AutoTest.Application.Dto;
using AutoTest.Assertions.Http;
using AutoTest.Core.Assertion;
using AutoTest.Assertions;

namespace AutoTest.Application.Builder.AssertionBuilder;

public class HttpAssertionMap : IAssertionMap
{
    private readonly IEnumerable<IField> _resolvers;
    private readonly IOperator _operators;
    public  HttpAssertionMap(IEnumerable<IField> resolvers, IOperator operators)
    {
        _resolvers = resolvers;
        _operators = operators;
    }
    public IAssertion Map(AssertionRule rule)
    {
        var dto = JsonSerializer.Deserialize<HttpAssertionDto>(rule.ConfigJson, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        })!;

        return new HttpAssertion(
            dto.Id,
            Enum.Parse<HttpAssertionField>(dto.Field, true),
            dto.HeaderKey,
            dto.Expected,            
            _resolvers,
            _operators
        );
    }
}

using System.Text.Json;
using AutoTest.Application;
using AutoTest.Application.Dto;
using AutoTest.Assertions;
using AutoTest.Assertions.Http;
using AutoTest.Core.Assertion;
using Microsoft.Extensions.Logging;

namespace AutoTest.Infrastructure.Mapper.AssertionMapper;

public sealed class HttpAssertionMap : IAssertionMap
{
    private readonly IEnumerable<IField> _resolvers;
    private readonly IOperator _operators;
    private readonly ILoggerFactory? _loggerFactory;

    public string Type => "HTTP";

    public HttpAssertionMap(IEnumerable<IField> resolvers, IOperator operators, ILoggerFactory? loggerFactory = null)
    {
        _resolvers = resolvers;
        _operators = operators;
        _loggerFactory = loggerFactory;
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
            _operators,
            _loggerFactory?.CreateLogger<HttpAssertion>()
        );
    }
}

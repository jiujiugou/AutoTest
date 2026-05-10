using System.Text.Json;
using AutoTest.Application;
using AutoTest.Application.Dto;
using AutoTest.Assertions.Tcp;
using AutoTest.Core.Assertion;
using Microsoft.Extensions.Logging;

namespace AutoTest.Infrastructure.Mapper.AssertionMapper;

public sealed class TcpAssertionMap : IAssertionMap
{
    private readonly ILoggerFactory? _loggerFactory;

    public string Type => "TCP";

    public TcpAssertionMap(ILoggerFactory? loggerFactory = null)
    {
        _loggerFactory = loggerFactory;
    }

    public IAssertion Map(AssertionRule rule)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        var dto = JsonSerializer.Deserialize<TcpAssertionDto>(rule.ConfigJson, options)!;

        if (!Enum.TryParse<TcpAssertionField>(dto.Field, true, out var field))
            throw new InvalidOperationException($"Invalid TCP assertion field: {dto.Field}");
        if (!Enum.TryParse<TcpAssertionOperator>(dto.Operator, true, out var op))
            throw new InvalidOperationException($"Invalid TCP assertion operator: {dto.Operator}");

        return new TcpAssertion(
            dto.Id,
            field,
            op,
            dto.Expected,
            _loggerFactory?.CreateLogger<TcpAssertion>()
        );
    }
}

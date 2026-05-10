using System.Text.Json;
using AutoTest.Application;
using AutoTest.Application.Dto;
using AutoTest.Assertion.Python;
using AutoTest.Core.Assertion;
using Microsoft.Extensions.Logging;

namespace AutoTest.Infrastructure.Mapper.AssertionMapper;

public sealed class PythonAssertionMap : IAssertionMap
{
    private readonly ILoggerFactory? _loggerFactory;

    public string Type => "PYTHON";

    public PythonAssertionMap(ILoggerFactory? loggerFactory = null)
    {
        _loggerFactory = loggerFactory;
    }

    public IAssertion Map(AssertionRule rule)
    {
        var dto = JsonSerializer.Deserialize<PythonAssertionDto>(rule.ConfigJson, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        })!;

        return new PythonAssertion(
            dto.Id,
            dto.Field,
            dto.Operator,
            dto.Expected,
            _loggerFactory?.CreateLogger<PythonAssertion>()
        );
    }
}

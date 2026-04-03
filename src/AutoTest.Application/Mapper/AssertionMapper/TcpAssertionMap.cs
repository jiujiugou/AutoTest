using System.Text.Json;
using AutoTest.Application.Dto;
using AutoTest.Assertions.Tcp;
using AutoTest.Core.Assertion;

namespace AutoTest.Application.Mapper.AssertionMapper;

public class TcpAssertionMap : IAssertionMap
{
    public IAssertion Map(AssertionRule rule)
    {
        var dto = JsonSerializer.Deserialize<TcpAssertionDto>(rule.ConfigJson, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        })!;

        return new TcpAssertion(
            dto.Id,
            dto.Field,
            dto.Operator,
            dto.Expected
        );
    }

}

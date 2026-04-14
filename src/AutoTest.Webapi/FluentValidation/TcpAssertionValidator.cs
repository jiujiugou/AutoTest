using System.Text.Json;
using AutoTest.Application.Dto;
using AutoTest.Assertions.Tcp;
using FluentValidation;

namespace AutoTest.Webapi.FluentValidation;

public class TcpAssertionValidator : AbstractValidator<AssertionDto>
{
    public TcpAssertionValidator()
    {
        Include(new AssertionDtoBaseValidator());

        RuleFor(x => x.ConfigJson)
            .Must(BeValidTcpAssertionJson)
            .WithMessage("ConfigJson 必须是有效的 TCP 断言 JSON，且包含 Field 和 Operator");
    }

    private bool BeValidTcpAssertionJson(string json)
    {
        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            var dto = JsonSerializer.Deserialize<TcpAssertionDto>(json, options);
            // 假设 TCP 断言至少有 Field、Operator 和 Expected 三个字段
            return dto != null
                   && !string.IsNullOrWhiteSpace(dto.Field)
                   && !string.IsNullOrWhiteSpace(dto.Operator)
                   && !string.IsNullOrWhiteSpace(dto.Expected)
                   && Enum.TryParse<TcpAssertionField>(dto.Field, true, out _)
                   && Enum.TryParse<TcpAssertionOperator>(dto.Operator, true, out _);
        }
        catch
        {
            return false;
        }
    }
}

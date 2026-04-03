using System.Text.Json;
using AutoTest.Application.Dto;
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
            var dto = JsonSerializer.Deserialize<TcpAssertionDto>(json);
            // 假设 TCP 断言至少有 Field、Operator 和 Value 三个字段
            return dto != null
                   && !string.IsNullOrEmpty(dto.Field.ToString())
                   && !string.IsNullOrEmpty(dto.Operator.ToString())
                   && !string.IsNullOrEmpty(dto.Expected.ToString());
        }
        catch
        {
            return false;
        }
    }
}

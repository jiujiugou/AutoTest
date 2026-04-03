using System.Text.Json;
using AutoTest.Application.Dto;
using FluentValidation;

namespace AutoTest.Webapi.FluentValidation;

public class HttpAssertionValidator : AbstractValidator<AssertionDto>
{
    public HttpAssertionValidator()
    {
        Include(new AssertionDtoBaseValidator());
        RuleFor(x => x.ConfigJson)
            .Must(BeValidHttpAssertionJson)
            .WithMessage("ConfigJson 必须是有效的 HTTP 断言 JSON，且包含 Field 和 Operator");
    }

    private bool BeValidHttpAssertionJson(string json)
    {
        try
        {
            var dto = JsonSerializer.Deserialize<HttpAssertionDto>(json);
            return dto != null && !string.IsNullOrEmpty(dto.Field.ToString()) && !string.IsNullOrEmpty(dto.Operator.ToString());
        }
        catch
        {
            return false;
        }
    }
}

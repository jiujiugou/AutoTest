using AutoTest.Application.Dto;
using FluentValidation;
namespace AutoTest.Webapi.FluentValidation;

public class AssertionDtoBaseValidator : AbstractValidator<AssertionDto>
{
    public AssertionDtoBaseValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Type).NotEmpty();
        RuleFor(x => x.ConfigJson).NotEmpty();
    }
}

using AutoTest.Application.Dto;
using FluentValidation;

namespace AutoTest.Webapi.FluentValidation;

public class TcpTargetValidator : AbstractValidator<TcpTargetDto>
{
    public TcpTargetValidator()
    {
        RuleFor(x => x.Host).NotEmpty().WithMessage("Host 不能为空");
        RuleFor(x => x.Port).InclusiveBetween(1, 65535).WithMessage("Port 必须在 1-65535 之间");
        RuleFor(x => x.ConnectTimeoutMs).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ReadTimeoutMs).GreaterThanOrEqualTo(0);
        RuleFor(x => x.WriteTimeoutMs).GreaterThanOrEqualTo(0);
        RuleFor(x => x.RetryCount).InclusiveBetween(0, 10);
        RuleFor(x => x.RetryDelayMs).InclusiveBetween(0, 30000);
    }
}

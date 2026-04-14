using FluentValidation;

namespace AutoTest.Webapi.Controllers
{
    public class LoginRequest
    {
        public string Username { get; set; } = null!;
        public string Password { get; set; }= null!;
    }
    public class LoginRequestValidator : AbstractValidator<LoginRequest>
    {
        public LoginRequestValidator()
        {
            RuleFor(x => x.Username)
                .NotEmpty()
                .Length(3, 50);

            RuleFor(x => x.Password)
                .NotEmpty()
                .MinimumLength(6);
        }
    }
}

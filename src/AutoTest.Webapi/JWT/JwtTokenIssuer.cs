using Auth;

namespace AutoTest.Webapi.JWT;

public sealed class JwtTokenIssuer : ITokenIssuer
{
    private readonly JwtService _jwtService;

    public JwtTokenIssuer(JwtService jwtService)
    {
        _jwtService = jwtService;
    }

    public string GenerateAccessToken(string subject, string role, IEnumerable<string>? permissions = null)
    {
        return _jwtService.GenerateToken(subject, role, permissions);
    }
}

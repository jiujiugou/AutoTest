using Auth;
using Microsoft.AspNetCore.Mvc;

namespace AutoTest.Webapi.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _auth;

        public AuthController(IAuthService auth)
        {
            _auth = auth;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginRequest req, CancellationToken cancellationToken)
        {

                var r = await _auth.LoginAsync(req.Username, req.Password, cancellationToken);
            if(r != null)
                return Ok(new { accessToken = r.AccessToken, refreshToken = r.RefreshToken });
            else
                return Unauthorized(new
                {
                    code = 401,
                    message = "Invalid username or password"
                });
            
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh(RefreshRequest req, CancellationToken cancellationToken)
        {
            
                var r = await _auth.RefreshAsync(req.RefreshToken, cancellationToken);
                if(r == null)
                {
                    return Unauthorized(new
                    {
                        message = "Invalid refresh token"
                    });
                }
                return Ok(new { accessToken = r.AccessToken, refreshToken = r.RefreshToken });
            
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout(RefreshRequest req, CancellationToken cancellationToken)
        {
            await _auth.LogoutAsync(req.RefreshToken, cancellationToken);
            return Ok();
        }

        [HttpPost("bootstrap")]
        public async Task<IActionResult> Bootstrap(BootstrapRequest req, CancellationToken cancellationToken)
        {
            try
            {
                await _auth.BootstrapAdminAsync(req.Username, req.Password, cancellationToken);
                return Ok();
            }
            catch (InvalidOperationException)
            {
                return Conflict(new { message = "Users already exist" });
            }
        }
    }

    public sealed class RefreshRequest
    {
        public string RefreshToken { get; set; } = null!;
    }

    public sealed class BootstrapRequest
    {
        public string Username { get; set; } = null!;
        public string Password { get; set; } = null!;
    }
}

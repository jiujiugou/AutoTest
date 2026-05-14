using Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AutoTest.Webapi.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize]
public sealed class AdminController : ControllerBase
{
    private readonly IAuthService _auth;
    private readonly IRbacService _rbac;

    public AdminController(IAuthService auth, IRbacService rbac)
    {
        _auth = auth;
        _rbac = rbac;
    }

    [HttpGet("users")]
    [Authorize(Policy = "perm:api.settings.view")]
    public async Task<IActionResult> Users([FromQuery] int take = 100)
    {
        var rows = await _rbac.GetUsersAsync(Math.Clamp(take, 1, 500));
        return Ok(rows);
    }

    [HttpPost("users")]
    [Authorize(Policy = "perm:api.settings.manage")]
    public async Task<IActionResult> CreateUser(CreateUserRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Username) || req.Username.Length < 3)
            return BadRequest(new { error = "用户名不能为空且至少3个字符" });
        if (string.IsNullOrWhiteSpace(req.Password) || req.Password.Length < 6)
            return BadRequest(new { error = "密码不能为空且至少6个字符" });
        if (string.IsNullOrWhiteSpace(req.Role))
            return BadRequest(new { error = "角色不能为空" });

        var userId = await _auth.CreateUserAsync(req.Username, req.Password, req.Role, HttpContext.RequestAborted);
        return Ok(new { id = userId });
    }

    [HttpPut("users/{userId:int}")]
    [Authorize(Policy = "perm:api.settings.manage")]
    public async Task<IActionResult> UpdateUser(int userId, UpdateUserRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Username))
            return BadRequest(new { error = "用户名不能为空" });

        await _auth.UpdateUserProfileAsync(userId, req.Username, req.IsActive, HttpContext.RequestAborted);
        return Ok();
    }

    [HttpDelete("users/{userId:int}")]
    [Authorize(Policy = "perm:api.settings.manage")]
    public async Task<IActionResult> DeleteUser(int userId)
    {
        await _auth.DeleteUserAsync(userId, HttpContext.RequestAborted);
        return Ok();
    }

    [HttpPut("users/{userId:int}/password")]
    [Authorize(Policy = "perm:api.settings.manage")]
    public async Task<IActionResult> ResetPassword(int userId, ResetPasswordRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Password) || req.Password.Length < 6)
            return BadRequest(new { error = "密码不能为空且至少6个字符" });

        await _auth.UpdateUserPasswordAsync(userId, req.Password, HttpContext.RequestAborted);
        return Ok();
    }

    public sealed class CreateUserRequest
    {
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
        public string Role { get; set; } = "";
    }

    public sealed class UpdateUserRequest
    {
        public string Username { get; set; } = "";
        public bool IsActive { get; set; } = true;
    }

    public sealed class ResetPasswordRequest
    {
        public string Password { get; set; } = "";
    }
}

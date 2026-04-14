using Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AutoTest.Webapi.Controllers;

/// <summary>
/// RBAC权限控制器
/// 用于管理角色、权限、用户之间的关系
/// </summary>
[ApiController]
[Route("api/rbac")]
[Authorize]
public sealed class RbacController : ControllerBase
{
    /// <summary>
    /// RBAC业务服务
    /// </summary>
    private readonly IRbacService _rbac;

    /// <summary>
    /// 构造函数（依赖注入IRbacService）
    /// </summary>
    public RbacController(IRbacService rbac)
    {
        _rbac = rbac;
    }

    // ================= 角色 =================

    /// <summary>
    /// 获取所有角色列表
    /// </summary>
    [HttpGet("roles")]
    [Authorize(Policy = "perm:settings.view")]
    public async Task<IActionResult> Roles()
    {
        var rows = await _rbac.GetRolesAsync();
        return Ok(rows);
    }

    // ================= 权限 =================

    /// <summary>
    /// 获取所有权限列表
    /// </summary>
    [HttpGet("permissions")]
    [Authorize(Policy = "perm:settings.view")]
    public async Task<IActionResult> Permissions()
    {
        var rows = await _rbac.GetPermissionsAsync();
        return Ok(rows);
    }

    // ================= 角色权限 =================

    /// <summary>
    /// 获取指定角色拥有的权限列表
    /// </summary>
    /// <param name="roleId">角色ID</param>
    [HttpGet("roles/{roleId:int}/permissions")]
    [Authorize(Policy = "perm:settings.view")]
    public async Task<IActionResult> RolePermissions(int roleId)
    {
        var rows = await _rbac.GetRolePermissionsAsync(roleId);
        return Ok(rows);
    }

    /// <summary>
    /// 设置角色权限（覆盖原有权限）
    /// </summary>
    /// <param name="roleId">角色ID</param>
    /// <param name="req">权限Code列表</param>
    [HttpPut("roles/{roleId:int}/permissions")]
    [Authorize(Policy = "perm:settings.manage")]
    public async Task<IActionResult> SetRolePermissions(int roleId, SetRolePermissionsRequest req)
    {
        await _rbac.SetRolePermissionsAsync(roleId, req.Codes ?? []);
        return Ok();
    }

    // ================= 用户 =================

    /// <summary>
    /// 获取用户列表（限制数量）
    /// </summary>
    /// <param name="take">返回数量（默认100，最大500）</param>
    [HttpGet("users")]
    [Authorize(Policy = "perm:settings.view")]
    public async Task<IActionResult> Users([FromQuery] int take = 100)
    {
        var rows = await _rbac.GetUsersAsync(Math.Clamp(take, 1, 500));
        return Ok(rows);
    }

    // ================= 用户角色 =================

    /// <summary>
    /// 获取用户当前绑定的角色
    /// </summary>
    /// <param name="userId">用户ID</param>
    [HttpGet("users/{userId:int}/role")]
    [Authorize(Policy = "perm:settings.view")]
    public async Task<IActionResult> UserRole(int userId)
    {
        var row = await _rbac.GetUserRoleAsync(userId);
        return Ok(row);
    }

    /// <summary>
    /// 设置用户角色（单角色绑定）
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="req">角色名称</param>
    [HttpPut("users/{userId:int}/role")]
    [Authorize(Policy = "perm:settings.manage")]
    public async Task<IActionResult> SetUserRole(int userId, SetUserRoleRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.RoleName))
            return BadRequest("角色名称不能为空");

        await _rbac.SetUserRoleAsync(userId, req.RoleName);
        return Ok();
    }

    // ================= DTO =================

    /// <summary>
    /// 设置角色权限请求
    /// </summary>
    public sealed class SetRolePermissionsRequest
    {
        /// <summary>
        /// 权限Code列表
        /// </summary>
        public string[]? Codes { get; set; }
    }

    /// <summary>
    /// 设置用户角色请求
    /// </summary>
    public sealed class SetUserRoleRequest
    {
        /// <summary>
        /// 角色名称
        /// </summary>
        public string RoleName { get; set; } = "";
    }
}
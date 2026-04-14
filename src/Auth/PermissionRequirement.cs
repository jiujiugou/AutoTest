using Microsoft.AspNetCore.Authorization;

namespace Auth;

/// <summary>
/// 封装单条权限名的授权需求
/// </summary>
public sealed class PermissionRequirement : IAuthorizationRequirement
{
    /// <summary>
    /// 创建需求
    /// </summary>
    /// <param name="permission">权限名，如 monitor.create</param>
    public PermissionRequirement(string permission)
    {
        Permission = permission;
    }

    /// <summary>
    /// 权限名
    /// </summary>
    public string Permission { get; }
}

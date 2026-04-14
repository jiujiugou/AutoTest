using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Auth;

/// <summary>
/// 权限授权处理器：基于用户 Claim 与权限存储做授权判定
/// </summary>
public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly IPermissionStore _store;
    private readonly PermissionOptions _options;

    /// <summary>
    /// 构造
    /// </summary>
    /// <param name="store">权限存储</param>
    /// <param name="options">权限配置</param>
    public PermissionAuthorizationHandler(IPermissionStore store, IOptions<PermissionOptions> options)
    {
        _store = store;
        _options = options.Value;
    }

    /// <summary>
    /// 授权执行：匿名直接拒绝；管理员角色直通；否则查询存储
    /// </summary>
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.User?.Identity?.IsAuthenticated != true)
            return;

        if (context.User.IsInRole(_options.AdminRole))
        {
            context.Succeed(requirement);
            return;
        }

        var userId = GetUserId(context.User);
        if (string.IsNullOrWhiteSpace(userId))
            return;

        if (await _store.HasPermissionAsync(userId, requirement.Permission, CancellationToken.None))
            context.Succeed(requirement);
    }

    /// <summary>
    /// 提取用户标识
    /// </summary>
    private string? GetUserId(ClaimsPrincipal user)
    {
        var id = user.FindFirstValue(_options.UserIdClaimType);
        if (!string.IsNullOrWhiteSpace(id))
            return id;

        id = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrWhiteSpace(id))
            return id;

        return user.FindFirstValue("sub");
    }
}

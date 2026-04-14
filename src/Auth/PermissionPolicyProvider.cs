using Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Auth;

/// <summary>
/// 动态策略提供器：将形如 "perm:xxx" 的策略名解析为 PermissionRequirement
/// </summary>
public sealed class PermissionPolicyProvider : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider _fallback;
    private readonly PermissionOptions _options;

    /// <summary>
    /// 使用框架默认策略提供器作为兜底
    /// </summary>
    public PermissionPolicyProvider(IOptions<AuthorizationOptions> authorizationOptions, IOptions<PermissionOptions> options)
    {
        _fallback = new DefaultAuthorizationPolicyProvider(authorizationOptions);
        _options = options.Value;
    }

    /// <summary>
    /// 根据策略名构建策略；仅当匹配前缀时自定义生成，否则走默认提供器
    /// </summary>
    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (policyName.StartsWith(_options.PolicyPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var permission = policyName.Substring(_options.PolicyPrefix.Length).Trim();
            if (permission.Length == 0)
                return Task.FromResult<AuthorizationPolicy?>(null);

            var policy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(permission))
                .Build();

            return Task.FromResult<AuthorizationPolicy?>(policy);
        }

        return _fallback.GetPolicyAsync(policyName);
    }

    /// <inheritdoc />
    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => _fallback.GetDefaultPolicyAsync();
    /// <inheritdoc />
    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => _fallback.GetFallbackPolicyAsync();
}

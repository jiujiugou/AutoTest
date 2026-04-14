using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Auth;

/// <summary>
/// 授权（Policy + Handler）依赖注入扩展
/// </summary>
public static class AddPermissionAuthorizationExtensions
{
    /// <summary>
    /// 注册基于 Dapper 的权限授权（动态策略 + 存储 + 处理器）
    /// </summary>
    public static IServiceCollection AddDapperPermissionAuthorization(this IServiceCollection services, Action<PermissionOptions>? configure = null)
    {
        services.AddMemoryCache();
        services.AddOptions<PermissionOptions>();
        if (configure != null)
            services.Configure(configure);

        services.TryAddEnumerable(ServiceDescriptor.Scoped<IAuthorizationHandler, PermissionAuthorizationHandler>());
        services.TryAddScoped<IPermissionStore, DapperPermissionStore>();

        services.AddAuthorization();
        services.Replace(ServiceDescriptor.Singleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>());

        return services;
    }
}

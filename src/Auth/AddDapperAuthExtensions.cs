using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
namespace Auth;

/// <summary>
/// 认证组件的依赖注入扩展
/// </summary>
public static class AddDapperAuthExtensions
{
    /// <summary>
    /// 注册基于 Dapper 的认证服务、存储与密码哈希
    /// </summary>
    public static IServiceCollection AddDapperAuth(this IServiceCollection services)
    {

        services.TryAddScoped<IAuthStore, DapperAuthStore>();
        services.TryAddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.TryAddScoped<IAuthService, DapperAuthService>();
        return services;
    }
}

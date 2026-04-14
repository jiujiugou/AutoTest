using System.Security.Cryptography;
using Microsoft.AspNetCore.WebUtilities;

namespace Auth;

/// <summary>
/// 基于 Dapper 存储的认证服务实现：登录/刷新/登出/初始化管理员
/// </summary>
public sealed class DapperAuthService : IAuthService
{
    private readonly IAuthStore _store;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenIssuer _tokenIssuer;

    /// <summary>
    /// 构造
    /// </summary>
    public DapperAuthService(IAuthStore store, IPasswordHasher passwordHasher, ITokenIssuer tokenIssuer)
    {
        _store = store;
        _passwordHasher = passwordHasher;
        _tokenIssuer = tokenIssuer;
    }

    /// <inheritdoc />
    public async Task BootstrapAdminAsync(string username, string password, CancellationToken cancellationToken)
    {
        var count = await _store.CountUsersAsync(cancellationToken);
        if (count != 0)
            throw new InvalidOperationException("Users already exist");

        var hash = _passwordHasher.Hash(password);
        await _store.CreateUserAsync(username, hash, "admin", DateTime.UtcNow, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<LoginResult> LoginAsync(string username, string password, CancellationToken cancellationToken)
    {
        var user = await _store.GetByUsernameAsync(username, cancellationToken);
        if (user == null || !user.IsActive)
            return null;

        if (!_passwordHasher.Verify(password, user.PasswordHash))
            return null;

        var accessToken = _tokenIssuer.GenerateAccessToken(user.Username, user.Role);
        var refreshToken = GenerateRefreshToken();
        await _store.AddRefreshTokenAsync(user.Id, refreshToken, DateTime.UtcNow.AddDays(30), DateTime.UtcNow, cancellationToken);
        return new LoginResult(accessToken, refreshToken);
    }

    /// <inheritdoc />
    public async Task<LoginResult> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var row = await _store.GetRefreshTokenAsync(refreshToken, cancellationToken);
        if (row == null || row.Revoked || row.ExpireAt <= DateTime.UtcNow)
            return null;

        var user = await _store.GetByIdAsync(row.UserId, cancellationToken);
        if (user == null || !user.IsActive)
            return null;

        var accessToken = _tokenIssuer.GenerateAccessToken(user.Username, user.Role);
        var newRefreshToken = GenerateRefreshToken();
        await _store.RevokeRefreshTokenAsync(refreshToken, newRefreshToken, cancellationToken);
        await _store.AddRefreshTokenAsync(user.Id, newRefreshToken, DateTime.UtcNow.AddDays(30), DateTime.UtcNow, cancellationToken);
        return new LoginResult(accessToken, newRefreshToken);
    }

    /// <inheritdoc />
    public async Task LogoutAsync(string refreshToken, CancellationToken cancellationToken)
    {
        await _store.RevokeRefreshTokenAsync(refreshToken, null, cancellationToken);
    }

    /// <summary>
    /// 生成随机 RefreshToken（URL 安全 Base64）
    /// </summary>
    private static string GenerateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(48);
        return WebEncoders.Base64UrlEncode(bytes);
    }
}

using Dapper;
using System;
using System.Collections.Generic;
using System.Data;
using System.Text;

namespace Identity
{
    /// <summary>
    /// 用户表实体（用于登录/权限相关查询）。
    /// </summary>
    public sealed class User
    {
        public int Id { get; set; }
        public string Username { get; set; } = "";
        public bool IsActive { get; set; }
        public string? DisplayName { get; set; }
    }

    /// <summary>
    /// 用户仓储（Dapper）：提供按用户名/ID 查询用户信息的能力。
    /// </summary>
    public class UserRepository
    {
        private readonly IDbConnection _conn;

        /// <summary>
        /// 初始化 <see cref="UserRepository"/>。
        /// </summary>
        /// <param name="conn">数据库连接。</param>
        public UserRepository(IDbConnection conn)
        {
            _conn = conn;
        }

        /// <summary>
        /// 根据用户名获取启用状态的用户。
        /// </summary>
        /// <param name="username">用户名。</param>
        /// <returns>用户；不存在则返回 null。</returns>
        public Task<User?> GetByUsernameAsync(string username)
        {
            return _conn.QueryFirstOrDefaultAsync<User>(
                "SELECT * FROM Users WHERE Username = @Username AND IsActive = 1",
                new { Username = username });
        }

        /// <summary>
        /// 根据用户 ID 获取用户。
        /// </summary>
        /// <param name="id">用户 ID。</param>
        /// <returns>用户；不存在则返回 null。</returns>
        public Task<User?> GetByIdAsync(int id)
        {
            return _conn.QueryFirstOrDefaultAsync<User>(
                "SELECT * FROM Users WHERE Id = @Id",
                new { Id = id });
        }
    }
}

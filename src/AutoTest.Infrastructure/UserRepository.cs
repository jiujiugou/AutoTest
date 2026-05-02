using AutoTest.Core.Auth;
using AutoTest.Core.Repositories;
using Dapper;
using System;
using System.Collections.Generic;
using System.Data;
using System.Text;

namespace Identity
{
    /// <summary>
    /// 用户仓储实现（Dapper）
    /// 负责用户数据的增删改查操作
    /// </summary>
    public class UserRepository : IUserRepository
    {
        private readonly IDbConnection _conn;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="conn">数据库连接（由 DI 注入）</param>
        public UserRepository(IDbConnection conn)
        {
            _conn = conn;
        }

        /// <summary>
        /// 根据用户名查询用户（用于登录 / 唯一性检查）
        /// 只返回启用状态用户（IsActive = 1）
        /// </summary>
        /// <param name="username">用户名</param>
        /// <returns>用户实体，不存在返回 null</returns>
        public Task<User?> GetByUsernameAsync(string username)
        {
            return _conn.QueryFirstOrDefaultAsync<User>(
                @"SELECT *
                  FROM Users
                  WHERE Username = @Username AND IsActive = 1",
                new { Username = username });
        }

        /// <summary>
        /// 根据用户ID查询用户（用于编辑 / RBAC绑定）
        /// </summary>
        /// <param name="id">用户ID</param>
        /// <returns>用户实体，不存在返回 null</returns>
        public Task<User?> GetByIdAsync(int id)
        {
            return _conn.QueryFirstOrDefaultAsync<User>(
                @"SELECT *
                  FROM Users
                  WHERE Id = @Id",
                new { Id = id });
        }

        /// <summary>
        /// 用户分页查询
        /// 用于后台用户列表展示
        /// </summary>
        /// <param name="page">页码（从1开始）</param>
        /// <param name="pageSize">每页数量</param>
        /// <returns>分页结果（数据 + 总数）</returns>
        public async Task<PagedResult<User>> ListAsync(int page, int pageSize)
        {
            var dataSql = @"
                SELECT *
                FROM Users
                ORDER BY Id DESC
                OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

            var countSql = "SELECT COUNT(*) FROM Users";

            var items = await _conn.QueryAsync<User>(
                dataSql,
                new
                {
                    Offset = (page - 1) * pageSize,
                    PageSize = pageSize
                });

            var total = await _conn.ExecuteScalarAsync<int>(countSql);

            return new PagedResult<User>
            {
                Items = items.ToList(),
                Total = total
            };
        }

        /// <summary>
        /// 创建用户（待实现）
        /// </summary>
        /// <param name="user">用户实体</param>
        /// <returns>新用户ID</returns>
        public Task<int> CreateAsync(User user)
        {
            // TODO: INSERT INTO Users ...
            throw new NotImplementedException();
        }

        /// <summary>
        /// 更新用户信息（待实现）
        /// 可用于：用户名、密码、状态、角色绑定等
        /// </summary>
        /// <param name="user">用户实体</param>
        /// <returns>是否更新成功</returns>
        public Task<bool> UpdateAsync(User user)
        {
            // TODO: UPDATE Users SET ...
            throw new NotImplementedException();
        }

        /// <summary>
        /// 删除用户（待实现）
        /// 建议改为软删除（IsActive = 0）
        /// </summary>
        /// <param name="id">用户ID</param>
        /// <returns>是否删除成功</returns>
        public Task<bool> DeleteAsync(int id)
        {
            // TODO: UPDATE Users SET IsActive = 0 WHERE Id = @Id
            throw new NotImplementedException();
        }
    }
}
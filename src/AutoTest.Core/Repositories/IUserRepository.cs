using AutoTest.Core.Auth;
using System;
using System.Collections.Generic;
using System.Text;

namespace AutoTest.Core.Repositories
{
    /// <summary>
    /// 用户仓储接口（User Repository）
    /// 用于封装所有用户相关的数据访问操作
    /// </summary>
    public interface IUserRepository
    {
        /// <summary>
        /// 根据用户ID获取用户信息
        /// 常用于：用户详情、编辑用户、RBAC绑定用户
        /// </summary>
        /// <param name="id">用户ID</param>
        /// <returns>用户实体，如果不存在则返回 null</returns>
        Task<User?> GetByIdAsync(int id);

        /// <summary>
        /// 根据用户名获取用户信息
        /// 常用于：登录验证、用户名唯一性校验
        /// </summary>
        /// <param name="username">用户名</param>
        /// <returns>用户实体，如果不存在则返回 null</returns>
        Task<User?> GetByUsernameAsync(string username);

        /// <summary>
        /// 用户分页查询
        /// 常用于：后台用户列表展示（支持分页）
        /// </summary>
        /// <param name="page">当前页码（从1开始）</param>
        /// <param name="pageSize">每页数量</param>
        /// <returns>分页结果（用户列表 + 总数）</returns>
        Task<PagedResult<User>> ListAsync(int page, int pageSize);

        /// <summary>
        /// 创建新用户
        /// 常用于：管理员新增用户
        /// </summary>
        /// <param name="user">用户实体</param>
        /// <returns>返回新用户ID</returns>
        Task<int> CreateAsync(User user);

        /// <summary>
        /// 更新用户信息
        /// </summary>
        Task<bool> UpdateAsync(User user);

        /// <summary>
        /// 软删除用户（设置 IsActive = 0）
        /// </summary>
        Task<bool> DeleteAsync(int id);
    }
}
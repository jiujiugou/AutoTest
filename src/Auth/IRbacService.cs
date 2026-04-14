using Auth.RBAC;
using System;
using System.Collections.Generic;
using System.Text;

namespace Auth
{
    /// <summary>
    /// RBAC 权限服务接口
    /// 提供角色、权限、用户绑定关系的查询与修改能力
    /// </summary>
    public interface IRbacService
    {
        /// <summary>
        /// 获取所有角色
        /// </summary>
        Task<IEnumerable<Role>> GetRolesAsync();

        /// <summary>
        /// 获取所有权限
        /// </summary>
        Task<IEnumerable<Permission>> GetPermissionsAsync();

        /// <summary>
        /// 获取某个角色的权限列表
        /// </summary>
        /// <param name="roleId">角色ID</param>
        Task<IEnumerable<RolePermission>> GetRolePermissionsAsync(int roleId);

        /// <summary>
        /// 获取用户列表
        /// </summary>
        /// <param name="take">返回数量限制</param>
        Task<IEnumerable<AuthUser>> GetUsersAsync(int take);

        /// <summary>
        /// 获取用户当前角色
        /// </summary>
        /// <param name="userId">用户ID</param>
        Task<UserRole?> GetUserRoleAsync(int userId);

        /// <summary>
        /// 设置角色权限（覆盖模式）
        /// </summary>
        /// <param name="roleId">角色ID</param>
        /// <param name="codes">权限Code集合</param>
        Task SetRolePermissionsAsync(int roleId, string[] codes);

        /// <summary>
        /// 设置用户角色（单角色模式）
        /// </summary>
        /// <param name="userId">用户ID</param>
        /// <param name="roleName">角色名称</param>
        Task SetUserRoleAsync(int userId, string roleName);
    }
}

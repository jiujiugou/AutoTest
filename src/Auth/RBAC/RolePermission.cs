using System;
using System.Collections.Generic;
using System.Text;

namespace Auth.RBAC
{
    /// <summary>
    /// 角色权限关系
    /// </summary>
    public sealed class RolePermission
    {
        /// <summary>角色ID</summary>
        public int RoleId { get; set; }

        /// <summary>权限ID</summary>
        public int PermissionId { get; set; }
    }
}

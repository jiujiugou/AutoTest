using System;
using System.Collections.Generic;
using System.Text;

namespace Auth.RBAC
{
    /// <summary>
    /// 用户角色关系
    /// </summary>
    public sealed class UserRole
    {
        /// <summary>用户ID</summary>
        public int UserId { get; set; }

        /// <summary>角色ID</summary>
        public int RoleId { get; set; }
    }
}

using System;
using System.Collections.Generic;
using System.Text;

namespace Auth.RBAC
{
    /// <summary>
    /// 角色实体（如 admin / user）
    /// </summary>
    public sealed class Role
    {
        /// <summary>角色主键</summary>
        public int Id { get; set; }

        /// <summary>角色名称</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>显示名称（给前端用）</summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>描述</summary>
        public string? Description { get; set; }
    }
}

using System;
using System.Collections.Generic;
using System.Text;

namespace Auth.RBAC
{
    /// <summary>
    /// 权限定义（如 monitor.create）
    /// </summary>
    public sealed class Permission
    {
        /// <summary>权限主键</summary>
        public int Id { get; set; }

        /// <summary>权限标识（唯一）</summary>
        public string Code { get; set; } = string.Empty;

        /// <summary>显示名称（给前端用）</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>描述（可选）</summary>
        public string? Description { get; set; }
    }
}

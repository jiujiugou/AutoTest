using System;
using System.Collections.Generic;
using System.Text;

namespace AutoTest.Core.Auth
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
    public class PagedResult<T>
    {
        public List<T> Items { get; set; } = new();
        public int Total { get; set; }
    }
}

using System;

namespace DynamicForm.Areas.Permission.Models
{
    /// <summary>
    /// 系統使用者。
    /// </summary>
    public class User
    {
        /// <summary>使用者唯一識別碼。</summary>
        public Guid Id { get; set; }

        /// <summary>使用者帳號。</summary>
        public string Account { get; set; } = string.Empty;

        /// <summary>使用者名稱。</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>密碼雜湊。</summary>
        public string PasswordHash { get; set; } = string.Empty;

        /// <summary>密碼雜湊用鹽值。</summary>
        public string PasswordSalt { get; set; } = string.Empty;
    }
}


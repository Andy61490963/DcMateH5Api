using System;

namespace DynamicForm.Areas.Security.Models
{
    /// <summary>
    /// 使用者帳號資訊
    /// </summary>
    public class UserAccount
    {
        /// <summary>
        /// 使用者唯一識別碼
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// 登入帳號
        /// </summary>
        public string Account { get; set; } = string.Empty;
        
        /// <summary>
        /// 使用者名稱
        /// </summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>
        /// 密碼雜湊值（對應資料表 SWD）
        /// </summary>
        public string PasswordHash { get; set; } = string.Empty;

        /// <summary>
        /// Base64 編碼的鹽值（對應資料表 SWD_SALT）
        /// </summary>
        public string PasswordSalt { get; set; } = string.Empty;
    }
}

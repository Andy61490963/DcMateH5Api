namespace DynamicForm.Areas.Permission.ViewModels.PermissionManagement
{
    /// <summary>
    /// 更新使用者請求。
    /// </summary>
    public class UpdateUserRequest
    {
        /// <summary>使用者帳號。</summary>
        public string Account { get; set; } = string.Empty;

        /// <summary>使用者名稱。</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>密碼雜湊。</summary>
        public string PasswordHash { get; set; } = string.Empty;

        /// <summary>密碼雜湊鹽值。</summary>
        public string PasswordSalt { get; set; } = string.Empty;
    }
}


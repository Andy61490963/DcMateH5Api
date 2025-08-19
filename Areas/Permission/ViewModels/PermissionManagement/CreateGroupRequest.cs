using System.ComponentModel.DataAnnotations;

namespace DynamicForm.Areas.Permission.ViewModels.PermissionManagement
{
    /// <summary>
    /// 建立群組的請求資料。
    /// </summary>
    public class CreateGroupRequest
    {
        /// <summary>
        /// 群組名稱。
        /// </summary>
        [Required]
        public string Name { get; set; } = string.Empty;
    }
}

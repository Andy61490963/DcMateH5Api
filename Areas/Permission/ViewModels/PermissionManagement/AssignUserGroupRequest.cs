using System;
using System.ComponentModel.DataAnnotations;

namespace DynamicForm.Areas.Permission.ViewModels.PermissionManagement
{
    /// <summary>
    /// 將使用者指派至群組的請求資料。
    /// </summary>
    public class AssignUserGroupRequest
    {
        /// <summary>
        /// 使用者 ID。
        /// </summary>
        [Required]
        public Guid UserId { get; set; }
    }
}

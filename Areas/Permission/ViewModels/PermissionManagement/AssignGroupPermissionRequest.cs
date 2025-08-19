using System;
using System.ComponentModel.DataAnnotations;

namespace DynamicForm.Areas.Permission.ViewModels.PermissionManagement
{
    /// <summary>
    /// 將權限指派至群組的請求資料。
    /// </summary>
    public class AssignGroupPermissionRequest
    {
        /// <summary>
        /// 權限 ID。
        /// </summary>
        [Required]
        public Guid PermissionId { get; set; }
    }
}

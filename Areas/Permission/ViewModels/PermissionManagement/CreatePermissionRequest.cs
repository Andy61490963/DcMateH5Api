using System.ComponentModel.DataAnnotations;
using ClassLibrary;

namespace DynamicForm.Areas.Permission.ViewModels.PermissionManagement
{
    /// <summary>
    /// 建立權限的請求資料。
    /// </summary>
    public class CreatePermissionRequest
    {
        /// <summary>
        /// 權限代碼，例如：FormDesigner.Edit。
        /// </summary>
        [Required]
        public ActionType Code { get; set; }
    }
}

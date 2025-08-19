using ClassLibrary;

namespace DynamicForm.Areas.Permission.Models
{
    /// <summary>
    /// 功能權限。
    /// </summary>
    public class PermissionModel
    {
        /// <summary>
        /// 權限唯一識別碼。
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// 權限代碼，例如：FormDesigner.Edit。
        /// </summary>
        public ActionType Code { get; set; }
    }
}

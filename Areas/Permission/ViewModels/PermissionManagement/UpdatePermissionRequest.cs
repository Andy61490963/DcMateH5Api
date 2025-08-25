using ClassLibrary;

namespace DcMateH5Api.Areas.Permission.ViewModels.PermissionManagement
{
    /// <summary>
    /// 更新權限請求。
    /// </summary>
    public class UpdatePermissionRequest
    {
        public ActionType Code { get; set; }
        
        public bool IsActive { get; set; }
    }
}


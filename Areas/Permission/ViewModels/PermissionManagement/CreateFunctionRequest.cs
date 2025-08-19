namespace DcMateH5Api.Areas.Permission.ViewModels.PermissionManagement
{
    /// <summary>
    /// 建立功能請求。
    /// </summary>
    public class CreateFunctionRequest
    {
        public string Name { get; set; }
        public string Area { get; set; }
        public string Controller { get; set; }
    }
}


namespace DynamicForm.Areas.Permission.ViewModels.PermissionManagement
{
    /// <summary>
    /// 更新功能請求。
    /// </summary>
    public class UpdateFunctionRequest
    {
        public string Name { get; set; }
        public string Area { get; set; }
        public string Controller { get; set; }
    }
}


namespace DcMateH5Api.Areas.Permission.ViewModels.PermissionManagement
{
    /// <summary>
    /// 更新群組請求。
    /// </summary>
    public class UpdateGroupRequest
    {
        public string Name { get; set; } = string.Empty;
        
        public bool IsActive { get; set; }
        
        public string? Description { get; set; }
    }
}


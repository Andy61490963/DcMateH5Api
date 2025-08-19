using System;

namespace DynamicForm.Areas.Permission.ViewModels.PermissionManagement
{
    /// <summary>
    /// 更新選單請求。
    /// </summary>
    public class UpdateMenuRequest
    {
        public Guid? ParentId { get; set; }
        public Guid? SysFunctionId { get; set; }
        public string? Name { get; set; }
        public int Sort { get; set; }
        public bool IsShare { get; set; }
    }
}


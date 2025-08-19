using System;

namespace DcMateH5Api.Areas.Permission.ViewModels.PermissionManagement
{
    /// <summary>
    /// 指派功能權限給群組的請求。
    /// </summary>
    public class AssignGroupFunctionPermissionRequest
    {
        public Guid FunctionId { get; set; }
        public Guid PermissionId { get; set; }
    }
}


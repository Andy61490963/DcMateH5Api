using ClassLibrary;
using DcMateH5Api.Areas.Permission.Interfaces;
using DcMateH5Api.Areas.Permission.Models;
using DcMateH5Api.Areas.Permission.ViewModels.PermissionManagement;
using Microsoft.AspNetCore.Mvc;
using DcMateH5Api.Helper;

namespace DcMateH5Api.Areas.Permission.Controllers
{
    /// <summary>
    /// 提供群組、權限、功能、選單以及其關聯設定的 API 介面。
    /// </summary>
    [Area("Permission")]
    [ApiController]
    [ApiExplorerSettings(GroupName = SwaggerGroups.Permission)]
    [Route("[area]/[controller]")]
    [Produces("application/json")]
    public class PermissionManagementController : ControllerBase
    {
        private readonly IPermissionService _permissionService;
        
        private static class Routes
        {
            public const string Permissions = "permissions";
        }

        public PermissionManagementController(IPermissionService permissionService)
        {
            _permissionService = permissionService;
        }
    }
}

using ClassLibrary;
using DcMateH5Api.Areas.Permission.Interfaces;
using DcMateH5Api.Areas.Permission.Models;
using DcMateH5Api.Areas.Permission.ViewModels.PermissionManagement;
using Microsoft.AspNetCore.Mvc;
using System;
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

        // 路由常數集中管理，避免魔法字串散落
        private static class Routes
        {
            public const string Groups              = "groups";
            public const string GroupById           = "groups/{id}";
            public const string GroupUsers          = "groups/{groupId}/users";
            public const string GroupUserById       = "groups/{groupId}/users/{userId}";

            public const string Permissions         = "permissions";
            public const string PermissionById      = "permissions/{id}";

            public const string Functions           = "functions";
            public const string FunctionById        = "functions/{id}";

            public const string Menus               = "menus";
            public const string MenuById            = "menus/{id}";

            public const string GroupFuncPerms      = "groups/{groupId}/function-permissions";
            public const string GroupFuncPermByIds  = "groups/{groupId}/functions/{functionId}/permissions/{permissionId}";
        }

        public PermissionManagementController(IPermissionService permissionService)
        {
            _permissionService = permissionService;
        }

        #region 群組 CRUD

        /// <summary>建立新的群組。</summary>
        [HttpPost(Routes.Groups)]
        [ProducesResponseType(typeof(Group), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<Group>> CreateGroup([FromBody] CreateGroupRequest request, CancellationToken ct)
        {
            if (await _permissionService.GroupNameExistsAsync(request.Name, ct))
            {
                return Conflict($"群組名稱已存在: {request.Name}");
            }

            var id = await _permissionService.CreateGroupAsync(request, ct);

            // 201 + Location
            return CreatedAtAction(nameof(GetGroup), new { id });
        }

        /// <summary>取得指定群組資訊。</summary>
        [HttpGet(Routes.GroupById)]
        [ProducesResponseType(typeof(Group), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<Group>> GetGroup([FromRoute] Guid id, CancellationToken ct)
        {
            var group = await _permissionService.GetGroupAsync(id, ct);
            return group is null ? NotFound() : Ok(group);
        }

        /// <summary>更新指定群組名稱。</summary>
        [HttpPut(Routes.GroupById)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateGroup([FromRoute] Guid id, [FromBody] UpdateGroupRequest request, CancellationToken ct)
        {
            // 前置檢查：不存在就 404，避免「成功但其實沒更新」
            if (await _permissionService.GetGroupAsync(id, ct) is null)
            {
                return NotFound();
            }

            if (await _permissionService.GroupNameExistsAsync(request.Name, ct, id))
            {
                return Conflict($"群組名稱已存在: {request.Name}");
            }

            await _permissionService.UpdateGroupAsync(id, request, ct);
            return NoContent();
        }

        /// <summary>停用指定群組。</summary>
        [HttpDelete(Routes.GroupById)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteGroup([FromRoute] Guid id, CancellationToken ct)
        {
            if (await _permissionService.GetGroupAsync(id, ct) is null)
                return NotFound();

            await _permissionService.DeleteGroupAsync(id, ct);
            return NoContent();
        }

        #endregion

        #region 權限 CRUD

        /// <summary>建立新的權限碼(數值必須為列舉值)。</summary>
        [HttpPost(Routes.Permissions)]
        [ProducesResponseType(typeof(PermissionModel), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<PermissionModel>> CreatePermission([FromBody] CreatePermissionRequest request, CancellationToken ct)
        {
            if (!System.Enum.IsDefined(typeof(ActionType), request.Code))
            {
                return ValidationProblem($"無效的列舉操作代碼: {request.Code}");
            }

            if (await _permissionService.PermissionCodeExistsAsync(request.Code, ct))
            {
                return Conflict($"權限碼已存在: {request.Code}");
            }

            var id = await _permissionService.CreatePermissionAsync(request, ct);
            
            return CreatedAtAction(nameof(GetPermission), new { id });
        }

        /// <summary>取得指定權限資訊。</summary>
        [HttpGet(Routes.PermissionById)]
        [ProducesResponseType(typeof(PermissionModel), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<PermissionModel>> GetPermission([FromRoute] Guid id, CancellationToken ct)
        {
            var permission = await _permissionService.GetPermissionAsync(id, ct);
            return permission is null ? NotFound() : Ok(permission);
        }

        /// <summary>更新指定權限碼。</summary>
        [HttpPut(Routes.PermissionById)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdatePermission([FromRoute] Guid id, [FromBody] UpdatePermissionRequest request, CancellationToken ct)
        {
            if (!System.Enum.IsDefined(typeof(ActionType), request.Code))
                return ValidationProblem($"Invalid ActionType: {request.Code}");

            if (await _permissionService.GetPermissionAsync(id, ct) is null)
                return NotFound();

            if (await _permissionService.PermissionCodeExistsAsync(request.Code, ct, id))
                return Conflict($"權限碼已存在: {request.Code}");

            await _permissionService.UpdatePermissionAsync(id, request, ct);
            return NoContent();
        }

        /// <summary>停用指定權限。</summary>
        [HttpDelete(Routes.PermissionById)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeletePermission([FromRoute] Guid id, CancellationToken ct)
        {
            if (await _permissionService.GetPermissionAsync(id, ct) is null)
                return NotFound();

            await _permissionService.DeletePermissionAsync(id, ct);
            return NoContent();
        }

        #endregion

        #region 功能 CRUD

        /// <summary>建立新的功能。</summary>
        [HttpPost(Routes.Functions)]
        [ProducesResponseType(typeof(Function), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<Function>> CreateFunction([FromBody] CreateFunctionRequest request, CancellationToken ct)
        {
            if (await _permissionService.FunctionNameExistsAsync(request.Name, ct))
                return Conflict($"功能名稱已存在: {request.Name}");

            var id = await _permissionService.CreateFunctionAsync(new Function
            {
                Name = request.Name,
                Area = request.Area,
                Controller = request.Controller
            }, ct);

            var result = new Function { Id = id, Name = request.Name, Area = request.Area, Controller = request.Controller };
            return CreatedAtAction(nameof(GetFunction), new { id }, result);
        }

        /// <summary>取得指定功能資訊。</summary>
        [HttpGet(Routes.FunctionById)]
        [ProducesResponseType(typeof(Function), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<Function>> GetFunction([FromRoute] Guid id, CancellationToken ct)
        {
            var function = await _permissionService.GetFunctionAsync(id, ct);
            return function is null ? NotFound() : Ok(function);
        }

        /// <summary>更新功能基本資訊。</summary>
        [HttpPut(Routes.FunctionById)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateFunction([FromRoute] Guid id, [FromBody] UpdateFunctionRequest request, CancellationToken ct)
        {
            if (await _permissionService.GetFunctionAsync(id, ct) is null)
                return NotFound();

            if (await _permissionService.FunctionNameExistsAsync(request.Name, ct, id))
                return Conflict($"功能名稱已存在: {request.Name}");

            await _permissionService.UpdateFunctionAsync(new Function
            {
                Id = id,
                Name = request.Name,
                Area = request.Area,
                Controller = request.Controller
            }, ct);

            return NoContent();
        }

        /// <summary>停用指定功能。</summary>
        [HttpDelete(Routes.FunctionById)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteFunction([FromRoute] Guid id, CancellationToken ct)
        {
            if (await _permissionService.GetFunctionAsync(id, ct) is null)
                return NotFound();

            await _permissionService.DeleteFunctionAsync(id, ct);
            return NoContent();
        }

        #endregion

        #region 選單 CRUD

        /// <summary>建立新的選單項目。</summary>
        [HttpPost(Routes.Menus)]
        [ProducesResponseType(typeof(Menu), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<Menu>> CreateMenu([FromBody] CreateMenuRequest request, CancellationToken ct)
        {
            if (await _permissionService.MenuNameExistsAsync(request.Name, request.ParentId, ct))
                return Conflict($"選單名稱已存在: {request.Name}");

            var id = await _permissionService.CreateMenuAsync(new Menu
            {
                ParentId = request.ParentId,
                SysFunctionId = request.SysFunctionId,
                Name = request.Name,
                Sort = request.Sort,
                IsShare = request.IsShare
            }, ct);

            var result = new Menu
            {
                Id = id,
                ParentId = request.ParentId,
                SysFunctionId = request.SysFunctionId,
                Name = request.Name,
                Sort = request.Sort,
                IsShare = request.IsShare
            };

            return CreatedAtAction(nameof(GetMenu), new { id }, result);
        }

        /// <summary>取得指定選單資訊。</summary>
        [HttpGet(Routes.MenuById)]
        [ProducesResponseType(typeof(Menu), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<Menu>> GetMenu([FromRoute] Guid id, CancellationToken ct)
        {
            var menu = await _permissionService.GetMenuAsync(id, ct);
            return menu is null ? NotFound() : Ok(menu);
        }

        /// <summary>更新選單資訊。</summary>
        [HttpPut(Routes.MenuById)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateMenu([FromRoute] Guid id, [FromBody] UpdateMenuRequest request, CancellationToken ct)
        {
            if (await _permissionService.GetMenuAsync(id, ct) is null)
                return NotFound();

            if (await _permissionService.MenuNameExistsAsync(request.Name, request.ParentId, ct, id))
                return Conflict($"選單名稱已存在: {request.Name}");

            await _permissionService.UpdateMenuAsync(new Menu
            {
                Id = id,
                ParentId = request.ParentId,
                SysFunctionId = request.SysFunctionId,
                Name = request.Name,
                Sort = request.Sort,
                IsShare = request.IsShare
            }, ct);

            return NoContent();
        }

        /// <summary>停用指定選單。</summary>
        [HttpDelete(Routes.MenuById)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteMenu([FromRoute] Guid id, CancellationToken ct)
        {
            if (await _permissionService.GetMenuAsync(id, ct) is null)
                return NotFound();

            await _permissionService.DeleteMenuAsync(id, ct);
            return NoContent();
        }

        #endregion

        #region 使用者與群組關聯

        /// <summary>指派使用者到指定群組。</summary>
        [HttpPost(Routes.GroupUsers)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> AssignUserToGroup([FromRoute] Guid groupId, [FromBody] AssignUserGroupRequest request, CancellationToken ct)
        {
            // 若要更嚴謹，可加：群組是否存在、使用者是否存在（需 service 支援）
            await _permissionService.AssignUserToGroupAsync(request.UserId, groupId, ct);
            return NoContent();
        }

        /// <summary>從群組中移除指定使用者。</summary>
        [HttpDelete(Routes.GroupUserById)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> RemoveUserFromGroup([FromRoute] Guid groupId, [FromRoute] Guid userId, CancellationToken ct)
        {
            await _permissionService.RemoveUserFromGroupAsync(userId, groupId, ct);
            return NoContent();
        }

        #endregion

        #region 群組與功能權限關聯

        /// <summary>為群組指派功能與權限關聯。</summary>
        [HttpPost(Routes.GroupFuncPerms)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> AssignGroupFunctionPermission([FromRoute] Guid groupId, [FromBody] AssignGroupFunctionPermissionRequest request, CancellationToken ct)
        {
            await _permissionService.AssignGroupFunctionPermissionAsync(groupId, request.FunctionId, request.PermissionId, ct);
            return NoContent();
        }

        /// <summary>移除群組與功能權限的關聯。</summary>
        [HttpDelete(Routes.GroupFuncPermByIds)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> RemoveGroupFunctionPermission([FromRoute] Guid groupId, [FromRoute] Guid functionId, [FromRoute] Guid permissionId, CancellationToken ct)
        {
            await _permissionService.RemoveGroupFunctionPermissionAsync(groupId, functionId, permissionId, ct);
            return NoContent();
        }

        #endregion
    }
}

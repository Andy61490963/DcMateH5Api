using ClassLibrary;
using DynamicForm.Areas.Permission.Models;
using DynamicForm.Areas.Permission.ViewModels.Menu;

namespace DynamicForm.Areas.Permission.Interfaces
{
    /// <summary>
    /// 提供群組與權限相關操作。
    /// </summary>
    public interface IPermissionService
    {
        // 群組
        Task<Guid> CreateGroupAsync(string name, CancellationToken ct);
        Task<Group?> GetGroupAsync(Guid id, CancellationToken ct);
        Task UpdateGroupAsync(Group group, CancellationToken ct);
        Task DeleteGroupAsync(Guid id, CancellationToken ct);
        Task<bool> GroupNameExistsAsync(string name, CancellationToken ct, Guid? excludeId = null);

        // 權限
        Task<Guid> CreatePermissionAsync(ActionType code, CancellationToken ct);
        Task<PermissionModel?> GetPermissionAsync(Guid id, CancellationToken ct);
        Task UpdatePermissionAsync(PermissionModel permission, CancellationToken ct);
        Task DeletePermissionAsync(Guid id, CancellationToken ct);
        Task<bool> PermissionCodeExistsAsync(ActionType code, CancellationToken ct, Guid? excludeId = null);

        // 功能
        Task<Guid> CreateFunctionAsync(Function function, CancellationToken ct);
        Task<Function?> GetFunctionAsync(Guid id, CancellationToken ct);
        Task UpdateFunctionAsync(Function function, CancellationToken ct);
        Task DeleteFunctionAsync(Guid id, CancellationToken ct);
        Task<bool> FunctionNameExistsAsync(string name, CancellationToken ct, Guid? excludeId = null);

        // 選單
        Task<Guid> CreateMenuAsync(Menu menu, CancellationToken ct);
        Task<Menu?> GetMenuAsync(Guid id, CancellationToken ct);
        Task UpdateMenuAsync(Menu menu, CancellationToken ct);
        Task DeleteMenuAsync(Guid id, CancellationToken ct);
        Task<bool> MenuNameExistsAsync(string name, Guid? parentId, CancellationToken ct, Guid? excludeId = null);
        Task<IEnumerable<MenuTreeItem>> GetUserMenuTreeAsync(Guid userId, CancellationToken ct);

        // 使用者與群組關聯
        Task AssignUserToGroupAsync(Guid userId, Guid groupId, CancellationToken ct);
        Task RemoveUserFromGroupAsync(Guid userId, Guid groupId, CancellationToken ct);

        // 群組與功能權限關聯
        Task AssignGroupFunctionPermissionAsync(Guid groupId, Guid functionId, Guid permissionId, CancellationToken ct);
        Task RemoveGroupFunctionPermissionAsync(Guid groupId, Guid functionId, Guid permissionId, CancellationToken ct);

        // 權限檢查
        Task<bool> UserHasControllerPermissionAsync(Guid userId, string area, string controller, int actionCode);
    }
}


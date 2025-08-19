using ClassLibrary;
using Dapper;
using DynamicForm.Areas.Permission.Interfaces;
using DynamicForm.Areas.Permission.Models;
using DynamicForm.Areas.Permission.ViewModels.Menu;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Memory;

namespace DynamicForm.Areas.Permission.Services
{
    /// <summary>
    /// 權限服務的實作類別，負責透過 Dapper 與資料庫交互，管理群組、權限、功能、選單及其關聯設定。
    /// 提供 CRUD 與權限檢查功能，並搭配快取提升效能。
    /// </summary>
    public class PermissionService : IPermissionService
    {
        private readonly IDbExecutor _db;
        private readonly SqlConnection _con;
        private readonly IMemoryCache _cache;
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

        /// <summary>
        /// 建構函式，注入資料庫連線與記憶體快取。
        /// </summary>
        public PermissionService(IDbExecutor db, SqlConnection con, IMemoryCache cache)
        {
            _db = db;
            _con = con;
            _cache = cache;
        }

        #region 群組 CRUD

        /// <summary>
        /// 建立新群組。
        /// </summary>
        /// <param name="name">群組名稱。</param>
        /// <returns>回傳新群組的唯一識別碼 (GUID)。</returns>
        public async Task<Guid> CreateGroupAsync(string name, CancellationToken ct)
        {
            var id = Guid.NewGuid();
            const string sql =
                @"/**/INSERT INTO SYS_GROUP (ID, NAME, IS_ACTIVE)
                  VALUES (@Id, @Name, 1)";
            await _db.ExecuteAsync(sql, new { Id = id, Name = name },  
                timeoutSeconds: 30,
                ct: ct);
            
            return id;
        }

        /// <summary>
        /// 取得指定群組資訊（僅限啟用中）。
        /// </summary>
        /// <param name="id">群組 ID。</param>
        /// <returns>群組資料，若不存在則回傳 null。</returns>
        public Task<Group?> GetGroupAsync(Guid id, CancellationToken ct)
        {
            const string sql = @"SELECT ID, NAME FROM SYS_GROUP WHERE ID = @Id AND IS_ACTIVE = 1";
            return _db.QueryFirstOrDefaultAsync<Group>(sql, new { Id = id },  
                timeoutSeconds: 30,
                ct: ct);
        }

        /// <summary>
        /// 更新群組名稱。
        /// </summary>
        public Task UpdateGroupAsync(Group group, CancellationToken ct)
        {
            const string sql = @"UPDATE SYS_GROUP SET NAME = @Name WHERE ID = @Id";
            return _db.ExecuteAsync(sql, new { group.Id, group.Name },  
                timeoutSeconds: 30,
                ct: ct);
        }

        /// <summary>
        /// 停用（軟刪除）群組。
        /// </summary>
        public Task DeleteGroupAsync(Guid id, CancellationToken ct)
        {
            const string sql = @"UPDATE SYS_GROUP SET IS_ACTIVE = 0 WHERE ID = @Id";
            return _db.ExecuteAsync(sql, new { Id = id },  
                timeoutSeconds: 30,
                ct: ct);
        }

        /// <summary>
        /// 檢查群組名稱是否已存在。
        /// </summary>
        public async Task<bool> GroupNameExistsAsync(string name, CancellationToken ct, Guid? excludeId = null)
        {
            const string sql =
                @"SELECT COUNT(1)
                    FROM SYS_GROUP
                    WHERE NAME = @Name AND IS_ACTIVE = 1
                      AND (@ExcludeId IS NULL OR ID <> @ExcludeId)";
            
            var count = await _db.ExecuteScalarAsync<int>(sql, new { Name = name, ExcludeId = excludeId },  
                timeoutSeconds: 30,
                ct: ct);
            return count > 0;
        }

        #endregion

        #region 權限 CRUD

        /// <summary>
        /// 建立新的權限碼。
        /// </summary>
        public async Task<Guid> CreatePermissionAsync(ActionType code, CancellationToken ct)
        {
            var id = Guid.NewGuid();
            const string sql =
                @"INSERT INTO SYS_PERMISSION (ID, CODE, IS_ACTIVE)
                  VALUES (@Id, @Code, 1)";
            
            await _db.ExecuteAsync(sql, new { Id = id, Code = code },  
                timeoutSeconds: 30,
                ct: ct);
            
            return id;
        }

        /// <summary>
        /// 取得指定權限資訊（僅限啟用中）。
        /// </summary>
        public Task<PermissionModel?> GetPermissionAsync(Guid id, CancellationToken ct)
        {
            const string sql = @"/**/SELECT ID, CODE FROM SYS_PERMISSION WHERE ID = @Id AND IS_ACTIVE = 1";
            return _db.QuerySingleOrDefaultAsync<PermissionModel?>(sql, new { Id = id },  
                timeoutSeconds: 30,
                ct: ct);
        }

        /// <summary>
        /// 更新權限碼。
        /// </summary>
        public Task UpdatePermissionAsync(PermissionModel permission, CancellationToken ct)
        {
            const string sql = @"/**/UPDATE SYS_PERMISSION SET CODE = @Code WHERE ID = @Id";
            return _db.ExecuteAsync(sql, new { Id = permission.Id, Code = permission.Code },  
                timeoutSeconds: 30,
                ct: ct);
        }

        /// <summary>
        /// 停用（軟刪除）權限。
        /// </summary>
        public Task DeletePermissionAsync(Guid id, CancellationToken ct)
        {
            const string sql = @"/**/UPDATE SYS_PERMISSION SET IS_ACTIVE = 0 WHERE ID = @Id";
            return _db.ExecuteAsync(sql, new { Id = id },  
                timeoutSeconds: 30,
                ct: ct);
        }

        /// <summary>
        /// 檢查權限碼是否已存在。
        /// </summary>
        public async Task<bool> PermissionCodeExistsAsync(ActionType code, CancellationToken ct, Guid? excludeId = null)
        {
            const string sql =
                @"/**/SELECT COUNT(1)
                    FROM SYS_PERMISSION
                    WHERE CODE = @Code AND IS_ACTIVE = 1
                      AND (@ExcludeId IS NULL OR ID <> @ExcludeId)";
            var count = await _db.ExecuteScalarAsync<int>(sql, new { Code = code, ExcludeId = excludeId },  
                timeoutSeconds: 30,
                ct: ct);
            
            return count > 0;
        }

        #endregion

        #region 功能 CRUD

        /// <summary>
        /// 建立新功能。
        /// </summary>
        public async Task<Guid> CreateFunctionAsync(Function function, CancellationToken ct)
        {
            var id = Guid.NewGuid();
            const string sql =
                @"/**/INSERT INTO SYS_FUNCTION (ID, NAME, AREA, CONTROLLER, DEFAULT_ENDPOINT, IS_DELETE)
                  VALUES (@Id, @Name, @Area, @Controller, @Default_endpoint, 0)";
            await _db.ExecuteAsync(sql, new
            {
                Id = id,
                function.Name,
                function.Area,
                function.Controller,
                Default_endpoint = function.DEFAULT_ENDPOINT
            },  
            timeoutSeconds: 30,
            ct: ct);
            return id;
        }

        /// <summary>
        /// 取得指定功能資訊（僅限未刪除）。
        /// </summary>
        public Task<Function?> GetFunctionAsync(Guid id, CancellationToken ct)
        {
            const string sql =
                @"/**/SELECT ID, NAME, AREA, CONTROLLER, DEFAULT_ENDPOINT, IS_DELETE
                  FROM SYS_FUNCTION
                  WHERE ID = @Id AND IS_DELETE = 0";
            return _db.QuerySingleOrDefaultAsync<Function?>(sql, new { Id = id },  
                timeoutSeconds: 30,
                ct: ct);
        }

        /// <summary>
        /// 更新功能資訊。
        /// </summary>
        public Task UpdateFunctionAsync(Function function, CancellationToken ct)
        {
            const string sql =
                @"UPDATE SYS_FUNCTION
                  SET NAME = @Name, AREA = @Area, CONTROLLER = @Controller, DEFAULT_ENDPOINT = @Default_endpoint
                  WHERE ID = @Id AND IS_DELETE = 0";
            return _db.ExecuteAsync(sql, new
            {
                function.Id,
                function.Name,
                function.Area,
                function.Controller,
                Default_endpoint = function.DEFAULT_ENDPOINT
            },  
            timeoutSeconds: 30,
            ct: ct);
        }

        /// <summary>
        /// 停用（軟刪除）功能。
        /// </summary>
        public Task DeleteFunctionAsync(Guid id, CancellationToken ct)
        {
            const string sql = @"UPDATE SYS_FUNCTION SET IS_DELETE = 1 WHERE ID = @Id";
            return _db.ExecuteAsync(sql, new { Id = id },  
                timeoutSeconds: 30,
                ct: ct);
        }

        /// <summary>
        /// 檢查功能名稱是否已存在。
        /// </summary>
        public async Task<bool> FunctionNameExistsAsync(string name, CancellationToken ct, Guid? excludeId = null)
        {
            const string sql =
                @"SELECT COUNT(1)
                    FROM SYS_FUNCTION
                    WHERE NAME = @Name AND IS_DELETE = 0
                      AND (@ExcludeId IS NULL OR ID <> @ExcludeId)";
            var count = await _db.ExecuteScalarAsync<int>(sql, new { Name = name, ExcludeId = excludeId },  
                timeoutSeconds: 30,
                ct: ct);
            return count > 0;
        }

        #endregion

        #region 選單 CRUD

        /// <summary>
        /// 建立新選單項目。
        /// </summary>
        public async Task<Guid> CreateMenuAsync(Menu menu, CancellationToken ct)
        {
            var id = Guid.NewGuid();
            const string sql =
                @"INSERT INTO SYS_MENU (ID, PARENT_ID, SYS_FUNCTION_ID, NAME, SORT, IS_SHARE, IS_DELETE)
                  VALUES (@Id, @ParentId, @FuncId, @Name, @Sort, @IsShare, 0)";
            await _db.ExecuteAsync(sql, new
            {
                Id = id,
                menu.ParentId,
                FuncId = menu.SysFunctionId,
                menu.Name,
                menu.Sort,
                menu.IsShare
            },  
            timeoutSeconds: 30,
            ct: ct);
            return id;
        }

        /// <summary>
        /// 取得指定選單資訊（僅限未刪除）。
        /// </summary>
        public Task<Menu?> GetMenuAsync(Guid id, CancellationToken ct)
        {
            const string sql =
                @"SELECT ID, PARENT_ID AS ParentId, SYS_FUNCTION_ID AS SysFunctionId,
                         NAME, SORT, IS_SHARE, IS_DELETE
                  FROM SYS_MENU
                  WHERE ID = @Id AND IS_DELETE = 0";
            return _db.QuerySingleOrDefaultAsync<Menu?>(sql, new { Id = id },  
                timeoutSeconds: 30,
                ct: ct);
        }

        /// <summary>
        /// 更新選單資訊。
        /// </summary>
        public Task UpdateMenuAsync(Menu menu, CancellationToken ct)
        {
            const string sql =
                @"UPDATE SYS_MENU
                  SET PARENT_ID = @ParentId,
                      SYS_FUNCTION_ID = @FuncId,
                      NAME = @Name,
                      SORT = @Sort,
                      IS_SHARE = @IsShare
                  WHERE ID = @Id AND IS_DELETE = 0";
            return _db.ExecuteAsync(sql, new
            {
                menu.Id,
                menu.ParentId,
                FuncId = menu.SysFunctionId,
                menu.Name,
                menu.Sort,
                menu.IsShare
            },  
            timeoutSeconds: 30,
            ct: ct);
        }

        /// <summary>
        /// 停用（軟刪除）選單項目。
        /// </summary>
        public Task DeleteMenuAsync(Guid id, CancellationToken ct)
        {
            const string sql = @"UPDATE SYS_MENU SET IS_DELETE = 1 WHERE ID = @Id";
            return _db.ExecuteAsync(sql, new { Id = id },  
                timeoutSeconds: 30,
                ct: ct);
        }

        /// <summary>
        /// 檢查同層級選單名稱是否重複。
        /// </summary>
        public async Task<bool> MenuNameExistsAsync(string name, Guid? parentId, CancellationToken ct, Guid? excludeId = null)
        {
            const string sql =
                @"SELECT COUNT(1)
                    FROM SYS_MENU
                    WHERE NAME = @Name AND IS_DELETE = 0
                      AND ((@ParentId IS NULL AND PARENT_ID IS NULL) OR PARENT_ID = @ParentId)
                      AND (@ExcludeId IS NULL OR ID <> @ExcludeId)";
            var count = await _db.ExecuteScalarAsync<int>(sql, new { Name = name, ParentId = parentId, ExcludeId = excludeId },  
                timeoutSeconds: 30,
                ct: ct);
            return count > 0;
        }

        /// <summary>
        /// 取得指定使用者可見的選單樹。
        /// </summary>
        public async Task<IEnumerable<MenuTreeItem>> GetUserMenuTreeAsync(Guid userId, CancellationToken ct)
        {
            var cacheKey = GetMenuCacheKey(userId);
            if (_cache.TryGetValue(cacheKey, out IEnumerable<MenuTreeItem> cached))
                return cached;

            const string sql = @"WITH BaseVisible AS (
SELECT m.ID
FROM SYS_MENU m
WHERE ISNULL(m.IS_DELETE, 0) = 0
  AND (
       m.IS_SHARE = 1
    OR EXISTS (
         SELECT 1
         FROM SYS_GROUP_FUNCTION_PERMISSION gfp
         JOIN SYS_USER_GROUP ug
           ON ug.SYS_GROUP_ID = gfp.SYS_GROUP_ID
          AND ug.SYS_USER_ID  = @UserId
         JOIN SYS_GROUP g
           ON g.ID = ug.SYS_GROUP_ID
          AND ISNULL(g.IS_ACTIVE, 1) = 1
         JOIN SYS_PERMISSION p
           ON p.ID = gfp.SYS_PERMISSION_ID
          AND ISNULL(p.IS_ACTIVE, 1) = 1
         JOIN SYS_FUNCTION f
           ON f.ID = gfp.SYS_FUNCTION_ID
          AND ISNULL(f.IS_DELETE, 0) = 0
         WHERE gfp.SYS_FUNCTION_ID = m.SYS_FUNCTION_ID
       )
  )
),
Tree AS (
    SELECT m.*
    FROM SYS_MENU m
    WHERE m.ID IN (SELECT ID FROM BaseVisible)

    UNION ALL

    SELECT parent.*
    FROM SYS_MENU parent
    JOIN Tree child ON child.PARENT_ID = parent.ID
    WHERE ISNULL(parent.IS_DELETE, 0) = 0
)
SELECT DISTINCT
    t.ID, t.PARENT_ID, t.SYS_FUNCTION_ID, t.NAME, t.SORT, t.IS_SHARE,
    f.AREA, f.CONTROLLER, f.DEFAULT_ENDPOINT
FROM Tree t
LEFT JOIN SYS_FUNCTION f ON f.ID = t.SYS_FUNCTION_ID
ORDER BY t.SORT, t.NAME
OPTION (MAXRECURSION 32);";

            var rows = (await _db.QueryAsync<MenuTreeItem>(
                sql, new { UserId = userId },
                timeoutSeconds: 30,
                ct: ct
                )).ToList();
            
            // 依 PARENT_ID 分組，這邊只有兩種狀況，一種是父節點為null，一種不為null
            var lookup = rows.ToLookup(r => r.PARENT_ID);
            
            // 掛子節點
            foreach (var item in rows)
            {
                var children = lookup[item.ID] // 找出所有以自己為父的節點
                    .OrderBy(c => c.SORT)
                    .ThenBy(c => c.NAME)
                    .ToList();

                item.Children = children; // 掛到 Children 屬性
            }

            // 根：沒有父或父不在 rows 清單（被刪除/被過濾）
            var idSet = rows.Select(x => x.ID).ToHashSet();
            var result = rows
                .Where(x => x.PARENT_ID == null || !idSet.Contains(x.PARENT_ID.Value)) // 孤兒節點會被升級成父節點
                .OrderBy(x => x.SORT)
                .ThenBy(x => x.NAME)
                .ToList();


            _cache.Set(cacheKey, result, CacheDuration);
            return result;
        }

        #endregion

        #region 使用者與群組關聯

        /// <summary>
        /// 將使用者加入指定群組，並清除使用者權限快取。
        /// </summary>
        public Task AssignUserToGroupAsync(Guid userId, Guid groupId, CancellationToken ct)
        {
            var id = Guid.NewGuid();
            const string sql =
                @"INSERT INTO SYS_USER_GROUP (ID, SYS_USER_ID, SYS_GROUP_ID)
                  VALUES (@Id, @UserId, @GroupId)";
            _cache.Remove(GetCacheKey(userId));
            _cache.Remove(GetMenuCacheKey(userId));
            return _db.ExecuteAsync(sql, new { Id = id, UserId = userId, GroupId = groupId },  
                timeoutSeconds: 30,
                ct: ct);
        }

        /// <summary>
        /// 從群組移除使用者，並清除使用者權限快取。
        /// </summary>
        public Task RemoveUserFromGroupAsync(Guid userId, Guid groupId, CancellationToken ct)
        {
            const string sql =
                @"DELETE FROM SYS_USER_GROUP
                  WHERE SYS_USER_ID = @UserId AND SYS_GROUP_ID = @GroupId";
            _cache.Remove(GetCacheKey(userId));
            _cache.Remove(GetMenuCacheKey(userId));
            // return _db.TxAsync(async (conn, tx, token) =>
            // {
            //     await _db.ExecuteAsync(conn, tx, sql, new { UserId = userId, GroupId = groupId },  
            //         timeoutSeconds: 30,
            //         ct: token);
            //     
            // }, ct: ct);
            return _db.ExecuteAsync(sql, new { UserId = userId, GroupId = groupId },  
                timeoutSeconds: 30,
                ct: ct);
        }

        #endregion

        #region 群組與功能權限關聯

        /// <summary>
        /// 建立群組與功能權限的關聯。
        /// </summary>
        public Task AssignGroupFunctionPermissionAsync(Guid groupId, Guid functionId, Guid permissionId, CancellationToken ct)
        {
            var id = Guid.NewGuid();
            const string sql =
                @"INSERT INTO SYS_GROUP_FUNCTION_PERMISSION (ID, SYS_GROUP_ID, SYS_FUNCTION_ID, SYS_PERMISSION_ID)
                  VALUES (@Id, @GroupId, @FunctionId, @PermissionId)";
            return _db.ExecuteAsync(sql, new
            {
                Id = id,
                GroupId = groupId,
                FunctionId = functionId,
                PermissionId = permissionId
            },  
            timeoutSeconds: 30,
            ct: ct);
        }

        /// <summary>
        /// 移除群組與功能權限的關聯。
        /// </summary>
        public Task RemoveGroupFunctionPermissionAsync(Guid groupId, Guid functionId, Guid permissionId, CancellationToken ct)
        {
            const string sql =
                @"DELETE FROM SYS_GROUP_FUNCTION_PERMISSION
                  WHERE SYS_GROUP_ID = @GroupId AND SYS_FUNCTION_ID = @FunctionId AND SYS_PERMISSION_ID = @PermissionId";
            return _db.ExecuteAsync(sql, new
            {
                GroupId = groupId,
                FunctionId = functionId,
                PermissionId = permissionId
            },  
            timeoutSeconds: 30,
            ct: ct);
        }

        #endregion

        #region 權限檢查

        /// <summary>
        /// 檢查使用者是否具有指定 Area、Controller、Action 的存取權限。
        /// 查詢結果會快取一段時間以提升效能。
        /// </summary>
        /// <param name="userId">使用者 ID。</param>
        /// <param name="area">Area 名稱。</param>
        /// <param name="controller">Controller 名稱。</param>
        /// <param name="actionCode">動作代碼 (ActionType 對應的整數值)。</param>
        /// <returns>若具有權限回傳 true，否則 false。</returns>
        public async Task<bool> UserHasControllerPermissionAsync(Guid userId, string area, string controller, int actionCode)
        {
            var cacheKey = $"perm:{userId}:{area}:{controller}:{actionCode}";
            if (_cache.TryGetValue(cacheKey, out bool ok)) return ok;

            const string sql =
                @"SELECT CASE WHEN EXISTS (
                      SELECT 1
                      FROM SYS_USER_GROUP ug
                      JOIN SYS_GROUP_FUNCTION_PERMISSION gfp
                        ON gfp.SYS_GROUP_ID = ug.SYS_GROUP_ID
                      JOIN SYS_FUNCTION f
                        ON f.ID = gfp.SYS_FUNCTION_ID
                       AND f.IS_DELETE = 0
                      JOIN SYS_PERMISSION p
                        ON p.ID = gfp.SYS_PERMISSION_ID
                      WHERE ug.SYS_USER_ID = @UserId
                        AND f.AREA       = @Area
                        AND f.CONTROLLER = @Controller
                        AND p.CODE       = @ActionCode
                    ) THEN 1 ELSE 0 END AS HasPermission;";

            var has = await _con.ExecuteScalarAsync<int>(sql, new
            {
                UserId = userId,
                Area = area ?? string.Empty,
                Controller = controller,
                ActionCode = actionCode
            }) > 0;

            _cache.Set(cacheKey, has, TimeSpan.FromSeconds(60));
            return has;
        }

        /// <summary>
        /// 取得使用者權限快取的快取鍵。
        /// </summary>
        private static string GetCacheKey(Guid userId) => $"user_permissions:{userId}";

        /// <summary>
        /// 取得使用者側邊選單的快取鍵。
        /// </summary>
        private static string GetMenuCacheKey(Guid userId) => $"user_menu:{userId}";

        #endregion
    }
}

using ClassLibrary;
using Dapper;
using DcMateH5Api.Areas.Permission.Interfaces;
using DcMateH5Api.Areas.Permission.Models;
using DcMateH5Api.Areas.Permission.ViewModels.Menu;
using DcMateH5Api.Areas.Permission.ViewModels.PermissionManagement;
using Microsoft.Data.SqlClient;
using DcMateH5Api.Services.Cache;
using DcMateH5Api.SqlHelper;
using DcMateH5Api.Areas.Permission.Mappers;

namespace DcMateH5Api.Areas.Permission.Services
{
    /// <summary>
    /// 權限服務的實作類別，負責透過 Dapper 與資料庫交互，管理群組、權限、功能、選單及其關聯設定。
    /// 提供 CRUD 與權限檢查功能，並搭配快取提升效能。
    /// </summary>
    public class PermissionService : IPermissionService
    {
        private readonly SQLGenerateHelper _sqlHelper;
        private readonly IDbExecutor _db;
        private readonly SqlConnection _con;
        private readonly ICacheService _cache; // Redis 快取服務

        /// <summary>
        /// 建構函式，注入資料庫連線與快取服務。
        /// </summary>
        public PermissionService(SQLGenerateHelper sqlHelper, IDbExecutor db, SqlConnection con, ICacheService cache)
        {
            _sqlHelper = sqlHelper;
            _db = db;
            _con = con;
            _cache = cache;
        }

        #region 群組 CRUD

        /// <summary>
        /// 建立新群組。
        /// </summary>
        /// <param name="request">群組名稱</param>
        /// <param name="ct"></param>
        /// <returns>群組識別碼</returns>
        public async Task<Guid> CreateGroupAsync(CreateGroupRequest request, CancellationToken ct)
        {
            var model = GroupMapper.MapperCreate(request);
            await _sqlHelper.InsertAsync(model, ct);
            return model.Id;
        }

        /// <summary>
        /// 取得指定群組資訊（僅限啟用中）
        /// </summary>
        /// <param name="id">群組id</param>
        /// <param name="ct"></param>
        /// <returns>找不到</returns>
        public Task<Group?> GetGroupAsync(Guid id, CancellationToken ct)
        {
            var where = new WhereBuilder<Group>()
                .AndEq(x => x.Id, id)
                .AndEq(x => x.IsActive, true)
                .AndNotDeleted();
            return _sqlHelper.SelectFirstOrDefaultAsync(where, ct);
        }

        /// <summary>
        /// 更新群組
        /// </summary>
        public Task UpdateGroupAsync(Guid id, UpdateGroupRequest request, CancellationToken ct)
        {
            var model = GroupMapper.MapperUpdate(id, request);
            return _sqlHelper.UpdateAllByIdAsync(model, UpdateNullBehavior.IgnoreNulls, ct);
        }

        /// <summary>
        /// 停用群組。
        /// </summary>
        public Task DeleteGroupAsync(Guid id, CancellationToken ct)
        {
            var where = new WhereBuilder<Group>()
                .AndEq(x => x.Id, id);
            return _sqlHelper.DeleteWhereAsync(where, ct);
        }

        /// <summary>
        /// 檢查群組名稱是否已存在。
        /// </summary>
        public async Task<bool> GroupNameExistsAsync(string name, CancellationToken ct, Guid? excludeId = null)
        {
            var where = new WhereBuilder<Group>()
                .AndEq(x => x.Name, name)
                .AndEq(x => x.IsActive, true)
                .AndNotDeleted();
            var list = await _sqlHelper.SelectWhereAsync(where, ct);
            return list.Any(g => !excludeId.HasValue || g.Id != excludeId.Value);
        }

        #endregion

        #region 權限 CRUD

        /// <summary>
        /// 建立新的權限碼。
        /// </summary>
        public async Task<Guid> CreatePermissionAsync(CreatePermissionRequest request, CancellationToken ct)
        {
            var model = PermissionMapper.MapperCreate(request);
            await _sqlHelper.InsertAsync(model, ct);
            return model.Id;
        }

        /// <summary>
        /// 取得指定權限資訊（僅限啟用中）。
        /// </summary>
        public Task<PermissionModel?> GetPermissionAsync(Guid id, CancellationToken ct)
        {
            var where = new WhereBuilder<PermissionModel>()
                .AndEq(x => x.Id, id)
                .AndEq(x => x.IsActive, true)
                .AndNotDeleted();
            return _sqlHelper.SelectFirstOrDefaultAsync(where, ct);
        }

        /// <summary>
        /// 更新權限碼。
        /// </summary>
        public Task UpdatePermissionAsync(Guid id, UpdatePermissionRequest request, CancellationToken ct)
        {
            var model = PermissionMapper.MapperUpdate(id, request);
            return _sqlHelper.UpdateAllByIdAsync(model, UpdateNullBehavior.IgnoreNulls, ct);
        }

        /// <summary>
        /// 停用（軟刪除）權限。
        /// </summary>
        public Task DeletePermissionAsync(Guid id, CancellationToken ct)
        {
            var where = new WhereBuilder<PermissionModel>()
                .AndEq(x => x.Id, id);
            return _sqlHelper.DeleteWhereAsync(where, ct);
        }

        /// <summary>
        /// 檢查權限碼是否已存在。
        /// </summary>
        public async Task<bool> PermissionCodeExistsAsync(ActionType code, CancellationToken ct, Guid? excludeId = null)
        {
            var where = new WhereBuilder<PermissionModel>()
                .AndEq(x => x.Code, code)
                .AndEq(x => x.IsActive, true)
                .AndNotDeleted();
            var list = await _sqlHelper.SelectWhereAsync(where, ct);
            return list.Any(p => !excludeId.HasValue || p.Id != excludeId.Value);
        }

        #endregion

        #region 功能 CRUD

        /// <summary>
        /// 建立新功能。
        /// </summary>
        public async Task<Guid> CreateFunctionAsync(CreateFunctionRequest request, CancellationToken ct)
        {
            var model = FunctionMapper.MapperCreate(request);
            await _sqlHelper.InsertAsync(model, ct);
            return model.Id;
        }

        /// <summary>
        /// 取得指定功能資訊（僅限未刪除）。
        /// </summary>
        public Task<Function?> GetFunctionAsync(Guid id, CancellationToken ct)
        {
            var where = new WhereBuilder<Function>()
                .AndEq(x => x.Id, id)
                .AndNotDeleted();
            return _sqlHelper.SelectFirstOrDefaultAsync(where, ct);
        }

        /// <summary>
        /// 更新功能資訊。
        /// </summary>
        public Task UpdateFunctionAsync(Guid id, UpdateFunctionRequest request, CancellationToken ct)
        {
            var model = FunctionMapper.MapperUpdate(id, request);
            return _sqlHelper.UpdateAllByIdAsync(model, UpdateNullBehavior.IgnoreNulls, ct);
        }

        /// <summary>
        /// 停用（軟刪除）功能。
        /// </summary>
        public Task DeleteFunctionAsync(Guid id, CancellationToken ct)
        {
            var where = new WhereBuilder<Function>()
                .AndEq(x => x.Id, id);
            return _sqlHelper.DeleteWhereAsync(where, ct);
        }

        /// <summary>
        /// 檢查功能名稱是否已存在。
        /// </summary>
        public async Task<bool> FunctionNameExistsAsync(string name, CancellationToken ct, Guid? excludeId = null)
        {
            var where = new WhereBuilder<Function>()
                .AndEq(x => x.Name, name)
                .AndNotDeleted();
            var list = await _sqlHelper.SelectWhereAsync(where, ct);
            return list.Any(p => !excludeId.HasValue || p.Id != excludeId.Value);
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
        public async Task<IEnumerable<MenuTreeItem>> GetUserMenuTreeAsync(Guid userId, CancellationToken ct) // 依使用者取得選單樹狀結構
        {
            // 統一使用 CacheHelper 與 CacheKeys 管理快取，避免魔法字串散落。
            var cached = await _cache.GetUserMenuAsync<IEnumerable<MenuTreeItem>>(userId, ct); // 依照規則取得使用者選單快取
            if (cached != null) return cached; // 若快取存在直接回傳

            const string sql = @"WITH BaseVisible AS ( -- 篩選使用者可見的選單
SELECT m.ID -- 選單ID
FROM SYS_MENU m -- 選單表
WHERE ISNULL(m.IS_DELETE, 0) = 0 -- 排除已刪除的選單
  AND ( -- 以下判斷選單是否可見
       m.IS_SHARE = 1 -- 若為共享選單則可見
    OR EXISTS ( -- 檢查使用者群組與功能權限
         SELECT 1 -- 只需判斷是否存在
         FROM SYS_GROUP_FUNCTION_PERMISSION gfp -- 群組功能權限表
         JOIN SYS_USER_GROUP ug -- 使用者與群組關聯表
           ON ug.SYS_GROUP_ID = gfp.SYS_GROUP_ID -- 以群組ID關聯
          AND ug.SYS_USER_ID  = @UserId -- 指定使用者ID
         JOIN SYS_GROUP g -- 群組表
           ON g.ID = ug.SYS_GROUP_ID -- 以群組ID連接
          AND ISNULL(g.IS_ACTIVE, 1) = 1 -- 群組必須為啟用狀態
         JOIN SYS_PERMISSION p -- 權限表
           ON p.ID = gfp.SYS_PERMISSION_ID -- 關聯權限ID
          AND ISNULL(p.IS_ACTIVE, 1) = 1 -- 權限必須為啟用狀態
         JOIN SYS_FUNCTION f -- 功能表
           ON f.ID = gfp.SYS_FUNCTION_ID -- 關聯功能ID
          AND ISNULL(f.IS_DELETE, 0) = 0 -- 功能不得被刪除
         WHERE gfp.SYS_FUNCTION_ID = m.SYS_FUNCTION_ID -- 必須為同一功能
       ) -- EXISTS 結束
  ) -- AND 條件結束
), -- BaseVisible CTE 結束
Tree AS ( -- 透過遞迴找出所有相關選單
    SELECT m.* -- 第一層：可見選單
    FROM SYS_MENU m -- 選單表
    WHERE m.ID IN (SELECT ID FROM BaseVisible) -- 限制於可見選單ID

    UNION ALL -- 合併遞迴結果

    SELECT parent.* -- 找出父選單
    FROM SYS_MENU parent -- 父選單表
    JOIN Tree child ON child.PARENT_ID = parent.ID -- 以子選單連接父選單
    WHERE ISNULL(parent.IS_DELETE, 0) = 0 -- 排除已刪除的父選單
)
SELECT DISTINCT -- 移除重複資料
    t.ID, t.PARENT_ID, t.SYS_FUNCTION_ID, t.NAME, t.SORT, t.IS_SHARE, -- 選單欄位
    f.AREA, f.CONTROLLER, f.DEFAULT_ENDPOINT -- 對應功能路徑
FROM Tree t -- 來源為遞迴結果
LEFT JOIN SYS_FUNCTION f ON f.ID = t.SYS_FUNCTION_ID -- 關聯功能表取得路徑資訊
ORDER BY t.SORT, t.NAME -- 依排序與名稱排序
OPTION (MAXRECURSION 32);"; // 限制遞迴層級避免無限迴圈

            var rows = (await _db.QueryAsync<MenuTreeItem>( // 執行 SQL 取得平面選單資料
                sql, new { UserId = userId }, // 傳入使用者參數
                timeoutSeconds: 30, // 設定逾時秒數
                ct: ct // 取消權杖
                )).ToList(); // 轉成清單以便處理

            var lookup = rows.ToLookup(r => r.PARENT_ID); // 依父節點ID分組

            foreach (var item in rows) // 逐一處理每個節點
            {
                var children = lookup[item.ID] // 找出所有以自己為父的節點
                    .OrderBy(c => c.SORT) // 依排序欄位排序
                    .ThenBy(c => c.NAME) // 再以名稱排序
                    .ToList(); // 轉成清單

                item.Children = children; // 將子節點掛到 Children 屬性
            }

            var idSet = rows.Select(x => x.ID).ToHashSet(); // 建立所有節點ID的集合
            var result = rows
                .Where(x => x.PARENT_ID == null || !idSet.Contains(x.PARENT_ID.Value)) // 找出根節點或孤兒節點
                .OrderBy(x => x.SORT) // 依排序欄位排序
                .ThenBy(x => x.NAME) // 再以名稱排序
                .ToList(); // 轉成清單

            await _cache.SetUserMenuAsync(userId, result, ct: ct); // 將結果寫入快取，使用統一鍵值命名
            return result; // 回傳樹狀結果
        }

        #endregion

        #region 使用者與群組關聯

        /// <summary>
        /// 將使用者加入指定群組，並清除使用者權限快取。
        /// </summary>
        public async Task AssignUserToGroupAsync(Guid userId, Guid groupId, CancellationToken ct) // 將使用者加入群組並清除快取
        {
            var id = Guid.NewGuid(); // 建立關聯資料的唯一識別碼
            const string sql =
                @"INSERT INTO SYS_USER_GROUP (ID, SYS_USER_ID, SYS_GROUP_ID) -- 插入關聯欄位
                  VALUES (@Id, @UserId, @GroupId)"; // 新增使用者與群組的關聯資料
            // 快取清除使用統一 Helper，避免重複與遺漏。
            await _cache.RemoveUserCachesAsync(userId, ct); // 同時清除使用者權限與選單快取
            await _db.ExecuteAsync(sql, new { Id = id, UserId = userId, GroupId = groupId }, // 執行插入資料的 SQL
                timeoutSeconds: 30, // 設定逾時秒數
                ct: ct); // 取消權杖
        }

        /// <summary>
        /// 從群組移除使用者，並清除使用者權限快取。
        /// </summary>
        public async Task RemoveUserFromGroupAsync(Guid userId, Guid groupId, CancellationToken ct) // 將使用者自群組移除並清除快取
        {
            const string sql =
                @"DELETE FROM SYS_USER_GROUP -- 從關聯表刪除資料
                  WHERE SYS_USER_ID = @UserId AND SYS_GROUP_ID = @GroupId"; // 指定刪除條件
            // 快取清除使用統一 Helper，避免重複與遺漏。
            await _cache.RemoveUserCachesAsync(userId, ct); // 同時清除使用者權限與選單快取
            await _db.ExecuteAsync(sql, new { UserId = userId, GroupId = groupId }, // 執行刪除資料的 SQL
                timeoutSeconds: 30, // 設定逾時秒數
                ct: ct); // 取消權杖
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
        public async Task<bool> UserHasControllerPermissionAsync(Guid userId, string area, string controller, int actionCode) // 檢查使用者是否擁有指定控制器的權限
        {
            // 使用統一的 CacheKeys 與 Helper，避免硬編碼的快取鍵。
            var cached = await _cache.GetControllerPermissionAsync(userId, area, controller, actionCode); // 嘗試從快取取得結果
            if (cached.HasValue) return cached.Value; // 若快取存在則直接回傳

            const string sql =
                @"SELECT CASE WHEN EXISTS ( -- 判斷是否存在符合條件的資料
                      SELECT 1 -- 只需判斷存在即可
                      FROM SYS_USER_GROUP ug -- 使用者與群組關聯表
                      JOIN SYS_GROUP_FUNCTION_PERMISSION gfp -- 群組功能權限表
                        ON gfp.SYS_GROUP_ID = ug.SYS_GROUP_ID -- 以群組ID關聯
                      JOIN SYS_FUNCTION f -- 功能表
                        ON f.ID = gfp.SYS_FUNCTION_ID -- 連接功能ID
                       AND f.IS_DELETE = 0 -- 功能不得被刪除
                      JOIN SYS_PERMISSION p -- 權限表
                        ON p.ID = gfp.SYS_PERMISSION_ID -- 連接權限ID
                      WHERE ug.SYS_USER_ID = @UserId -- 指定使用者
                        AND f.AREA       = @Area -- 指定區域
                        AND f.CONTROLLER = @Controller -- 指定控制器
                        AND p.CODE       = @ActionCode -- 指定動作代碼
                    ) THEN 1 ELSE 0 END AS HasPermission;"; // 若存在則回傳1否則0

            var has = await _con.ExecuteScalarAsync<int>(sql, new // 執行 SQL 判斷是否有權限
            {
                UserId = userId, // 使用者ID參數
                Area = area ?? string.Empty, // 區域參數，若為 null 則給空字串
                Controller = controller, // 控制器名稱參數
                ActionCode = actionCode // 動作代碼參數
            }) > 0; // 大於0代表有權限

            await _cache.SetControllerPermissionAsync(userId, area, controller, actionCode, has, TimeSpan.FromSeconds(60)); // 將結果寫入快取並設定TTL
            return has; // 回傳檢查結果
        }

        #endregion
    }
}

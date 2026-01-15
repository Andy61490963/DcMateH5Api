using DCMATEH5API.Areas.Menu.Models;
using DcMateH5Api.DbExtensions; // 依據你同事的命名空間調整
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DCMATEH5API.Areas.Menu.Services
{
    public class MenuService : IMenuService
    {
        private readonly IDbExecutor _db;
        public MenuService(IDbExecutor db) { _db = db; }

        public async Task<List<MenuNavigationViewModel>> GetMenuTreeAsync(string userId)
        {
            string sql = @"
               -- 1. 來自選單節點
 SELECT 
     CAST(H5_ADM_MENU_MODULE_SID AS VARCHAR(36)) AS Id, 
     CAST(PARENT_ID AS VARCHAR(36)) AS ParentId, 
     MENU_NAME AS Title, 
     URL AS Url, 
     PARAMETER AS Parameter,
     0 AS Lv, 
     SEQ AS SortOrder,
     'MENU' AS SourceType,
     IMGICON AS ImgIcon -- 新增欄位
 FROM H5_ADM_MENU_MODULE

 UNION ALL

 -- 2. 來自頁面模組
 SELECT 
     CAST(P.H5_ADM_PAGE_MODULE_SID AS VARCHAR(36)) AS Id, 
     CAST(L.H5_ADM_MENU_MODULE_SID AS VARCHAR(36)) AS ParentId, 
     P.TITLE AS Title, 
     P.URL AS Url, 
     NULL AS Parameter,
     P.LV AS Lv, 
     P.SEQ AS SortOrder,
     'PAGE' AS SourceType,
     P.IMGICON AS ImgIcon -- 新增欄位
 FROM H5_ADM_PAGE_MODULE P
 JOIN H5_ADM_MENU_PAGE_LINK L ON P.H5_ADM_PAGE_MODULE_SID = L.H5_ADM_PAGE_MODULE_SID";

            var rawData = await _db.QueryAsync<MenuNavigationViewModel>(sql, new { UserId = userId });
            string rootGuid = "00000000-0000-0000-0000-000000000000";

            return BuildTree(rawData.ToList(), rootGuid);
        }

        private List<MenuNavigationViewModel> BuildTree(List<MenuNavigationViewModel> source, string parentId)
        {
            return source
                .Where(x => x.ParentId.Equals(parentId, StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => x.SortOrder)
                .Select(x => {
                    x.Children = BuildTree(source, x.Id);
                    return x;
                }).ToList();
        }
    }
}
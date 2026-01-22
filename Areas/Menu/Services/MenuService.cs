using DCMATEH5API.Areas.Menu.Models;
using DcMateH5Api.DbExtensions;
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

        /// <summary>
        /// 取得原始樹狀結構 (用於內部邏輯)
        /// </summary>
        public async Task<List<MenuNavigationViewModel>> GetMenuTreeAsync(string userId)
        {
            // 這裡已將 H5_ 移除，並對齊最新的欄位名稱
            string sql = @"
        -- 1. 使用 CTE 遞迴找出該帳號授權的所有目錄層級
        WITH UserAllowedMenus AS (
            -- 錨點：從群組權限表抓出該使用者的起始選單
            SELECT M.*
            FROM ADM_MENU_MODULE M
            INNER JOIN ADM_USERGROUP_MENU_LIST GML ON M.ADM_MENU_MODULE_SID = GML.ADM_MENU_MODULE_SID
            INNER JOIN ADM_USERGROUP_USER_LIST GUL ON GML.GROUP_SID = GUL.GROUP_SID
            INNER JOIN ADM_USER U ON GUL.USER_SID = U.USER_SID
            WHERE U.ACCOUNT_NO = @Account  -- 對應傳入的 userId

            UNION ALL

            -- 遞迴：找出這些目錄下方的所有子目錄
            SELECT M.*
            FROM ADM_MENU_MODULE M
            INNER JOIN UserAllowedMenus Parent ON M.PARENT_ID = Parent.ADM_MENU_MODULE_SID
        )

        -- 2. 合併目錄與頁面
        -- A. 撈出所有遞迴找到的目錄 (MENU)
        SELECT 
            CAST(ADM_MENU_MODULE_SID AS VARCHAR(36)) AS Id, 
            CAST(PARENT_ID AS VARCHAR(36)) AS ParentId, 
            ADM_MENU_MODULE_NAME AS Title, 
            URL AS Url, 
            PARAMETER AS Parameter, 
            [DESC] AS [Desc], 
            0 AS Lv, 
            SEQ AS SortOrder,
            'MENU' AS SourceType,
            IMGICON AS ImgIcon
        FROM UserAllowedMenus

        UNION ALL

        -- B. 撈出所有掛在這些目錄下的頁面 (PAGE)
        SELECT 
            CAST(P.ADM_PAGE_MODULE_SID AS VARCHAR(36)) AS Id, 
            CAST(L.ADM_MENU_MODULE_SID AS VARCHAR(36)) AS ParentId, 
            P.TITLE AS Title, 
            P.URL AS Url, 
            P.PARAMETER AS Parameter, 
            P.[DESC] AS [Desc], 
            P.LV AS Lv, 
            L.SEQ AS SortOrder, 
            'PAGE' AS SourceType, 
            P.IMGICON AS ImgIcon
        FROM ADM_PAGE_MODULE P
        INNER JOIN ADM_MENU_PAGE_LINK L ON P.ADM_PAGE_MODULE_SID = L.ADM_PAGE_MODULE_SID
        INNER JOIN UserAllowedMenus M ON L.ADM_MENU_MODULE_SID = M.ADM_MENU_MODULE_SID";

            // 執行查詢，將 userId 對應到 SQL 的 @Account
            var rawData = await _db.QueryAsync<MenuNavigationViewModel>(sql, new { Account = userId });


            // 根節點 GUID 預設值
            string rootGuid = "00000000-0000-0000-0000-000000000000";

            return BuildTree(rawData.ToList(), rootGuid);
        }

        /// <summary>
        /// 遞迴組裝樹狀結構
        /// </summary>
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

        /// <summary>
        /// 取得前端專用格式的 MenuList 與 PageList
        /// </summary>
        public async Task<MenuResponse> GetFullMenuByLvAsync(string userId)
        {
            var tree = await GetMenuTreeAsync(userId);
            var response = new MenuResponse();

            var rootPage = new PageFolderViewModel
            {
                Title = "首頁",
                Url = "index.html",
                Tiles = new List<TileViewModel>()
            };

            // 呼叫遞迴填充
            FillPagesRecursive(tree, response, rootPage.Tiles);

            // 將 index.html 入口加入
            response.MenuList.Add("index.html", rootPage);

            return response;
        }

        /// <summary>
        /// 遞迴平鋪所有選單與頁面至 Response
        /// </summary>
        private void FillPagesRecursive(List<MenuNavigationViewModel> nodes, MenuResponse response, List<TileViewModel> parentTiles)
        {
            foreach (var node in nodes)
            {
                var targetUrl = string.IsNullOrEmpty(node.Url) ? $"{node.Title}/index.html" : node.Url;

                // 加入目前層級的磁磚清單
                parentTiles.Add(new TileViewModel
                {
                    Sid = node.Id,
                    Title = node.Title,
                    Url = targetUrl,
                    Parameter = node.Parameter,
                    Property = node.SourceType,
                    ImgIcon = node.ImgIcon,
                    Seq = node.SortOrder,
                    Lv = node.Lv
                });

                // 如果是 MENU (目錄)，則需要在 MenuList 中獨立長出對應的 Dictionary 內容
                if (node.SourceType == "MENU")
                {
                    var folder = new PageFolderViewModel
                    {
                        Sid = node.Id,
                        Title = node.Title,
                        Url = node.Url,
                        Parameter = node.Parameter,
                        Property = node.SourceType,
                        ImgIcon = node.ImgIcon,
                        Lv = node.Lv,
                        Tiles = new List<TileViewModel>()
                    };

                    // 往下遞迴處理子項
                    if (node.Children != null && node.Children.Any())
                    {
                        FillPagesRecursive(node.Children, response, folder.Tiles);
                    }

                    // 加入字典
                    if (!response.MenuList.ContainsKey(targetUrl))
                        response.MenuList.Add(targetUrl, folder);
                }
                else if (node.SourceType == "PAGE")
                {
                    // 如果是 PAGE，加入全域扁平化清單 (方便前端搜尋)
                    response.PageList.Add(new TileViewModel
                    {
                        Sid = node.Id,
                        Title = node.Title,
                        Url = node.Url,
                        Parameter = node.Parameter,
                        Property = node.SourceType,
                        ImgIcon = node.ImgIcon
                    });
                }
            }
        }
    }
}
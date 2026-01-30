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
                -- 1. 來自選單節點 (目錄架構)
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
                FROM ADM_MENU_MODULE

                UNION ALL

                -- 2. 來自頁面模組 (實際磁磚內容)
                SELECT 
                    CAST(P.ADM_PAGE_MODULE_SID AS VARCHAR(36)) AS Id, 
                    CAST(L.ADM_MENU_MODULE_SID AS VARCHAR(36)) AS ParentId, 
                    P.TITLE AS Title, 
                    P.URL AS Url, 
                    P.PARAMETER AS Parameter, 
                    P.[DESC] AS [Desc], 
                    P.LV AS Lv, 
                    P.SEQ AS SortOrder,
                    'PAGE' AS SourceType,
                    P.IMGICON AS ImgIcon
                FROM ADM_PAGE_MODULE P
                JOIN ADM_MENU_PAGE_LINK L ON P.ADM_PAGE_MODULE_SID = L.ADM_PAGE_MODULE_SID";

            // 執行查詢
            var rawData = await _db.QueryAsync<MenuNavigationViewModel>(sql, new { UserId = userId });

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
        public async Task<MenuResponse> GetFullMenuByLvAsync(int lv)
        {
            var tree = await GetMenuTreeAsync("");
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
        private void FillPagesRecursive(List<MenuNavigationViewModel> nodes, MenuResponse response, List<TileViewModel> parentTiles, string currentBackUrl = "")
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
                    BackUrl = currentBackUrl, // ⭐ 設定返回的路徑
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
                        BackUrl = currentBackUrl, // ⭐ 目錄物件也記錄返回路徑
                        Parameter = node.Parameter,
                        Property = node.SourceType,
                        ImgIcon = node.ImgIcon,
                        Lv = node.Lv,
                        Tiles = new List<TileViewModel>()
                    };

                    // 往下遞迴處理子項
                    if (node.Children != null && node.Children.Any())
                    {
                        //FillPagesRecursive(node.Children, response, folder.Tiles);
                        // ⭐ 關鍵：將「目前的 targetUrl」傳入下一層，當作子項的「返回路徑」
                        FillPagesRecursive(node.Children, response, folder.Tiles, targetUrl);
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
                        BackUrl = currentBackUrl, // 頁面同樣記錄返回路徑
                        Parameter = node.Parameter,
                        Property = node.SourceType,
                        ImgIcon = node.ImgIcon
                    });
                }
            }
        }
    }
}
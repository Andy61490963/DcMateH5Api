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
     PARAMETER AS Parameter, -- 原本就有
     [DESC] AS [Description], -- 欄位名由 DESCRIPTION 改為 DESC
     0 AS Lv, 
     SEQ AS SortOrder,
     'MENU' AS SourceType,
     IMGICON AS ImgIcon
 FROM H5_ADM_MENU_MODULE

 UNION ALL

 -- 2. 來自頁面模組
 SELECT 
     CAST(P.H5_ADM_PAGE_MODULE_SID AS VARCHAR(36)) AS Id, 
     CAST(L.H5_ADM_MENU_MODULE_SID AS VARCHAR(36)) AS ParentId, 
     P.TITLE AS Title, 
     P.URL AS Url, 
     P.PARAMETER AS Parameter, -- 新增欄位：現在頁面也能帶參數了
     P.[DESC] AS [Description], -- 欄位名由 DESCRIPTION 改為 DESC
     P.LV AS Lv, 
     P.SEQ AS SortOrder,
     'PAGE' AS SourceType,
     P.IMGICON AS ImgIcon
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
        public async Task<MenuResponse> GetFullMenuByLvAsync(int lv)
        {
            // 1. 取得原始樹狀資料 (已包含層級關係)
            var tree = await GetMenuTreeAsync("");
            var response = new MenuResponse();

            // 2. 手動建立「首頁」節點 (Hardcode index.html)
            var rootPage = new PageFolderViewModel
            {
                Title = "首頁",
                Url = "index.html",
                PageKind = "MENU",
                Sid = "", // 首頁通常沒有實體 SID
                Lv = 0,
                Tiles = new List<TileViewModel>()
            };

            foreach (var node in tree)
            {
                // --- 規則 A：如果 node 是最頂層 (ParentId 為 0000...) ---
                // 將它轉換為「首頁」裡面的磁磚 (Tiles)
                rootPage.Tiles.Add(new TileViewModel
                {
                    Sid = node.Id,
                    Title = node.Title,
                    Url = node.Url,
                    ImgIcon = node.ImgIcon,
                    Seq = node.SortOrder,
                    Lv = node.Lv,
                    Property = "MENU"
                });

                // --- 規則 B：為每個頂層節點建立它自己的頁面入口 ---
                // 這是為了讓使用者點擊「KANBAN」後，能進入該目錄查看內部的 PAGE
                var folderKey = string.IsNullOrEmpty(node.Url) ? $"{node.Title}/index.html" : node.Url;

                var folder = new PageFolderViewModel
                {
                    Sid = node.Id,
                    Title = node.Title,
                    Url = node.Url,
                    ImgIcon = node.ImgIcon,
                    Lv = node.Lv,
                    Tiles = node.Children.Select(c => new TileViewModel
                    {
                        Sid = c.Id,
                        Title = c.Title,
                        Url = c.Url,
                        ImgIcon = c.ImgIcon,
                        Seq = c.SortOrder,
                        Lv = c.Lv,
                        Property = c.SourceType // 區分是 MENU 還是 PAGE
                    }).ToList()
                };

                // 加入字典 (例如：MES-ADM/index.html)
                if (!response.Pages.ContainsKey(folderKey))
                    response.Pages.Add(folderKey, folder);

                // --- 規則 C：同時收集所有 SourceType 為 PAGE 的到全域 PageList ---
                foreach (var child in node.Children.Where(x => x.SourceType == "PAGE"))
                {
                    response.PageList.Add(new TileViewModel
                    {
                        Sid = child.Id,
                        Title = child.Title,
                        Url = child.Url,
                        ImgIcon = child.ImgIcon
                    });
                }
            }

            // 最後：把最重要的「首頁」塞進 Dictionary 最前面
            response.Pages.Add("index.html", rootPage);

            return response;
        }

    }
}
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
            var tree = await GetMenuTreeAsync("");
            var response = new MenuResponse();

            // 1. 手動建立「首頁」
            var rootPage = new PageFolderViewModel { Title = "首頁", Url = "index.html", Tiles = new List<TileViewModel>() };

            foreach (var node in tree)
            {
                // --- 修正 A：首頁磁磚 (Tiles) 也要帶入 Parameter ---
                rootPage.Tiles.Add(new TileViewModel
                {
                    Sid = node.Id,
                    Title = node.Title,
                    Url = node.Url,
                    Parameter = node.Parameter, // <-- 加入這一行
                    Property = node.SourceType,
                    ImgIcon = node.ImgIcon,
                    Seq = node.SortOrder,
                    Lv = node.Lv
                });

                var folderKey = string.IsNullOrEmpty(node.Url) ? $"{node.Title}/index.html" : node.Url;

                // --- 修正 B：目錄頁面 (Folder) 帶入 Parameter ---
                var folder = new PageFolderViewModel
                {
                    Sid = node.Id,
                    Title = node.Title,
                    Url = node.Url,
                    Parameter = node.Parameter, // <-- 加入這一行
                    Property = node.SourceType,
                    ImgIcon = node.ImgIcon,
                    Lv = node.Lv,
                    // --- 修正 C：子頁面磁磚 (Tiles) 帶入 Parameter ---
                    Tiles = node.Children.Select(c => new TileViewModel
                    {
                        Sid = c.Id,
                        Title = c.Title,
                        Url = c.Url,
                        Parameter = c.Parameter, // <-- 加入這一行
                        Property = c.SourceType,
                        ImgIcon = c.ImgIcon,
                        Seq = c.SortOrder,
                        Lv = c.Lv
                    }).ToList()
                };

                if (!response.Pages.ContainsKey(folderKey))
                    response.Pages.Add(folderKey, folder);

                // --- 修正 D：全域 PageList 也要帶入 Parameter ---
                foreach (var child in node.Children.Where(x => x.SourceType == "PAGE"))
                {
                    response.PageList.Add(new TileViewModel
                    {
                        Sid = child.Id,
                        Title = child.Title,
                        Url = child.Url,
                        Parameter = child.Parameter, // <-- 加入這一行
                        Property = child.SourceType, // <-- 關鍵：這裡必須是 PAGE
                        ImgIcon = child.ImgIcon
                    });
                }
            }

            response.Pages.Add("index.html", rootPage);
            return response;
        }

    }
}
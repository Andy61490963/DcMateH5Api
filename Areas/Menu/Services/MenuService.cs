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

            // 2. 呼叫遞迴函式來填充 response.Pages 與 rootPage.Tiles
            FillPagesRecursive(tree, response, rootPage.Tiles);

            response.Pages.Add("index.html", rootPage);
            return response;
        }

        // 新增遞迴輔助方法
        private void FillPagesRecursive(List<MenuNavigationViewModel> nodes, MenuResponse response, List<TileViewModel> parentTiles)
        {
            foreach (var node in nodes)
            {
                // 統一生成 Key 值邏輯
                var targetUrl = string.IsNullOrEmpty(node.Url) ? $"{node.Title}/index.html" : node.Url;

                // 加入父層的磁磚清單
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

                // 核心修正：只要是 MENU，不論在哪一層，都要在 Pages 字典長出內容
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

                    // 繼續往下層遞迴處理子項目，並將結果存入目前 folder 的 Tiles 中
                    if (node.Children != null && node.Children.Any())
                    {
                        FillPagesRecursive(node.Children, response, folder.Tiles);
                    }

                    if (!response.Pages.ContainsKey(targetUrl))
                        response.Pages.Add(targetUrl, folder);
                }
                else if (node.SourceType == "PAGE")
                {
                    // 如果是 PAGE，則加入全域的 PageList
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
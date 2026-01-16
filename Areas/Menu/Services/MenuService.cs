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
            // 1. 抓取所有該等級可見的原始資料 (直接複用您現有的 SQL 思路，但不卡 UserId，卡 LV)
            var tree = await GetMenuTreeAsync(""); // 這裡可以根據需求修改為根據 LV 查詢的 SQL

            var response = new MenuResponse();

            // 2. 將樹狀結構轉換為前端需要的 Pages 字典格式
            foreach (var node in tree)
            {
                // 假設每個根節點（如：生產管理）對應一個 index.html 頁面
                var pageKey = string.IsNullOrEmpty(node.Url) ? "index.html" : node.Url;

                var pageViewModel = new PageFolderViewModel
                {
                    Sid = node.Id,
                    Title = node.Title,
                    Url = node.Url,
                    ImgIcon = node.ImgIcon,
                    Desc = node.Desc,
                    Lv = node.Lv,
                    // 關鍵：將子節點轉換為 Tiles (磁磚按鈕)
                    Tiles = node.Children.Select(c => new TileViewModel
                    {
                        Sid = c.Id,
                        Title = c.Title,
                        Url = c.Url,
                        ImgIcon = c.ImgIcon,
                        Desc = c.Desc,
                        Lv = c.Lv,
                        Seq = c.SortOrder
                    }).ToList()
                };

                response.Pages.Add(pageKey, pageViewModel);
            }

            return response;
        }

    }
}
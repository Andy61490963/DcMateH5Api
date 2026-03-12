using DbExtensions.DbExecutor.Interface;
using DcMateH5.Abstractions.Menu;
using DCMATEH5API.Areas.Menu.Models;

namespace DcMateH5.Infrastructure.Menu;

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
        //string rootGuid = "00000000-0000-0000-0000-000000000000";

        // ⭐ 關鍵：傳入 null，因為 Root 節點在資料庫裡 PARENT_ID 是 NULL
        return BuildTree(rawData, null);
    }

    /// <summary>
    /// 遞迴組裝樹狀結構
    /// </summary>
    private List<MenuNavigationViewModel> BuildTree(List<MenuNavigationViewModel> source, string? parentId)
    {
        // 標準化搜尋 ID
        string? normalizedSearchId = string.IsNullOrWhiteSpace(parentId) ? null : parentId.Trim().ToLower();

        return source
            .Where(x => {
                // 標準化當前節點的 ParentId
                string? nodeParentId = string.IsNullOrWhiteSpace(x.ParentId) ? null : x.ParentId.Trim().ToLower();

                // 如果是在找最頂層 (parentId 為 null)
                if (normalizedSearchId == null)
                {
                    // 這裡要抓 ParentId 是真正的 null (或是因為 Model 預設值沒被蓋掉而顯示為 null 字串的情況)
                    return nodeParentId == null || nodeParentId == "null";
                }

                // 如果是在找子層 (例如找 ParentId 為 0000... 的節點)
                return nodeParentId == normalizedSearchId;
            })
            .OrderBy(x => x.SortOrder)
            .Select(x => {
                // ⭐ 預防死循環：如果 ID 跟 ParentId 一樣，就不往下找了
                if (x.Id.Trim().ToLower() == x.ParentId?.Trim().ToLower()) return x;

                x.Children = BuildTree(source, x.Id);
                return x;
            }).ToList();
    }
    /// <summary>
    /// 取得前端專用格式的 MenuList 與 PageList
    /// </summary>
    public async Task<MenuResponse> GetFullMenuByLvAsync(int lv)
    {
        var allNodes = await GetMenuTreeAsync("");
        var response = new MenuResponse();

        var rootPage = new PageFolderViewModel
        {
            Title = "首頁",
            Url = "index.html",
            Tiles = new List<TileViewModel>()
        };

        // 1. 先找出 Root 那一筆 (0000...)
        var rootNode = allNodes.FirstOrDefault(x => x.Id.Trim().Contains("00000000"));

        // 2. 決定哪些人要出現在首頁磁磚
        // 如果有 Root 就拿它的小孩；如果沒 Root (對不到) 就拿 ParentId 為空的那些人
        var startNodes = (rootNode != null && rootNode.Children.Any())
                         ? rootNode.Children
                         : allNodes.Where(x => string.IsNullOrWhiteSpace(x.ParentId) || x.ParentId == "null").ToList();

        // 3. 執行填充 (這會同步填充 PageList)
        FillPagesRecursive(startNodes, response, rootPage.Tiles, "index.html");

        if (!response.MenuList.ContainsKey("index.html"))
            response.MenuList.Add("index.html", rootPage);

        return response;
    }

    /// <summary>
    /// 遞迴平鋪所有選單與頁面至 Response
    /// </summary>
    private void FillPagesRecursive(List<MenuNavigationViewModel> nodes, MenuResponse response, List<TileViewModel> parentTiles, string currentBackUrl)
    {
        foreach (var node in nodes)
        {
            var targetUrl = string.IsNullOrEmpty(node.Url) ? $"{node.Title}/index.html" : node.Url;

            // 產生磁磚
            parentTiles.Add(new TileViewModel
            {
                Sid = node.Id,
                Title = node.Title,
                Url = targetUrl,
                BackUrl = currentBackUrl, // ⭐ 設定正確的回上頁路徑
                Parameter = node.Parameter,
                Property = node.SourceType,
                ImgIcon = node.ImgIcon,
                Seq = node.SortOrder,
                Lv = node.Lv
            });

            if (node.SourceType == "MENU")
            {
                var folder = new PageFolderViewModel
                {
                    Sid = node.Id,
                    Title = node.Title,
                    Url = targetUrl,
                    BackUrl = currentBackUrl,
                    Parameter = node.Parameter,
                    Property = node.SourceType,
                    ImgIcon = node.ImgIcon,
                    Lv = node.Lv,
                    Tiles = new List<TileViewModel>()
                };

                if (node.Children != null && node.Children.Any())
                {
                    // 往下遞迴：目前的 targetUrl 變成下一層的 BackUrl
                    FillPagesRecursive(node.Children, response, folder.Tiles, targetUrl);
                }

                if (!response.MenuList.ContainsKey(targetUrl))
                    response.MenuList.Add(targetUrl, folder);
            }
            else if (node.SourceType == "PAGE")
            {
                response.PageList.Add(new TileViewModel
                {
                    Sid = node.Id,
                    Title = node.Title,
                    Url = targetUrl,
                    BackUrl = currentBackUrl,
                    Parameter = node.Parameter,
                    Property = node.SourceType,
                    ImgIcon = node.ImgIcon
                });
            }
        }
    }
}

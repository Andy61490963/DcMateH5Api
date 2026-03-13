using DbExtensions.DbExecutor.Interface;
using DcMateH5.Abstractions.Menu;
using DcMateH5.Abstractions.Menu.Models;

namespace DcMateH5.Infrastructure.Menu;

/// <summary>
/// 選單服務
/// </summary>
public class MenuService : IMenuService
{
    private static class MenuConstants
    {
        public const string MenuType = "MENU";
        public const string PageType = "PAGE";
    }

    private readonly IDbExecutor _db;

    public MenuService(IDbExecutor db)
    {
        _db = db;
    }

    /// <summary>
    /// 取得 legacy AuthInfo 格式的選單與頁面資料 ( 2026/03/13 Jack要求改為他制定的Json格式 )
    /// </summary>
    /// <param name="lv">頁面層級</param>
    /// <param name="userId">使用者識別</param>
    /// <returns>Legacy AuthInfo</returns>
    public async Task<AuthInfo> GetFullMenuByLvAsync(int lv, Guid userId)
    {
        _ = lv;
        _ = userId;

        List<MenuRowModel> menuRows = await GetMenuRowsAsync().ConfigureAwait(false);
        List<PageRowModel> pageRows = await GetPageRowsAsync().ConfigureAwait(false);

        Abstractions.Menu.Models.Menu[] menus = MapMenus(menuRows);
        Page[] pages = MapPages(pageRows);

        AuthInfo authInfo = new AuthInfo
        {
            MmenuList = new MenuList
            {
                Menuls = menus
            },
            PageList = new PageList
            {
                Pages = pages
            }
        };

        return authInfo;
    }

    /// <summary>
    /// 取得主選單與子選單資料
    /// </summary>
    /// <returns>選單資料列</returns>
    private async Task<List<MenuRowModel>> GetMenuRowsAsync()
    {
        const string sql = @"
SELECT
    D.ADM_MENU_MODULE_SID AS ParentSid,
    D.URL AS ParentUrl,
    C.MENU_SID AS MenuSid,
    C.MENU_NAME AS MenuName,
    C.URL AS MenuUrl,
    C.PARAMETER AS MenuParameter,
    C.SEQ AS MenuSeq,
    C.[DESC] AS MenuDesc,
    C.IMGICON AS MenuImgIcon,
    C.[SUB_MENU_SID] AS SubMenuSid,
    C.[SUB_MENU_NAME] AS SubMenuName,
    C.[SUB_URL] AS SubMenuUrl,
    C.[SUB_PARAMETER] AS SubMenuParameter,
    C.[SUB_SEQ] AS SubMenuSeq,
    C.[SUB_DESC] AS SubMenuDesc,
    C.[SUB_IMGICON] AS SubMenuImgIcon
FROM
(
    SELECT
        A.PARENT_ID AS ParentId,
        A.ADM_MENU_MODULE_SID AS MENU_SID,
        A.ADM_MENU_MODULE_NAME AS MENU_NAME,
        A.URL,
        A.PARAMETER,
        A.SEQ,
        A.[DESC],
        A.IMGICON,
        B.ADM_MENU_MODULE_SID AS SUB_MENU_SID,
        B.ADM_MENU_MODULE_NAME AS SUB_MENU_NAME,
        B.URL AS SUB_URL,
        B.PARAMETER AS SUB_PARAMETER,
        B.SEQ AS SUB_SEQ,
        B.[DESC] AS SUB_DESC,
        B.IMGICON AS SUB_IMGICON
    FROM ADM_MENU_MODULE A
    LEFT JOIN ADM_MENU_MODULE B
        ON B.PARENT_ID = A.ADM_MENU_MODULE_SID
) C
LEFT JOIN ADM_MENU_MODULE D
    ON C.ParentId = D.ADM_MENU_MODULE_SID
ORDER BY
    C.ParentId,
    C.SEQ,
    C.SUB_SEQ;";

        IEnumerable<MenuRowModel> result = await _db
            .QueryAsync<MenuRowModel>(sql, new { })
            .ConfigureAwait(false);

        return result.ToList();
    }

    /// <summary>
    /// 取得頁面資料
    /// </summary>
    /// <returns>頁面資料列</returns>
    private async Task<List<PageRowModel>> GetPageRowsAsync()
    {
        const string sql = @"
SELECT
    A.ADM_PAGE_MODULE_SID AS PageSid,
    A.TITLE AS Title,
    A.URL AS Url,
    A.PARAMETER AS Parameter,
    A.[DESC] AS [Desc],
    A.LV AS Lv,
    A.SEQ AS Seq,
    A.IMGICON AS ImgIcon,
    B.ADM_MENU_MODULE_SID AS MenuSid,
    C.ADM_MENU_MODULE_NAME AS MenuName,
    C.URL AS MenuUrl
FROM ADM_PAGE_MODULE A
INNER JOIN ADM_MENU_PAGE_LINK B
    ON A.ADM_PAGE_MODULE_SID = B.ADM_PAGE_MODULE_SID
INNER JOIN ADM_MENU_MODULE C
    ON B.ADM_MENU_MODULE_SID = C.ADM_MENU_MODULE_SID
ORDER BY
    B.ADM_MENU_MODULE_SID,
    A.SEQ;";

        IEnumerable<PageRowModel> result = await _db
            .QueryAsync<PageRowModel>(sql, new { })
            .ConfigureAwait(false);

        return result.ToList();
    }

    /// <summary>
    /// 將查詢資料映射為 legacy Menu[]
    /// </summary>
    /// <param name="rows">選單資料列</param>
    /// <returns>legacy Menu 陣列</returns>
    private static Abstractions.Menu.Models.Menu[] MapMenus(List<MenuRowModel> rows)
    {
        List<Abstractions.Menu.Models.Menu> menus = rows
            .GroupBy(x => x.MenuSid)
            .Select(group =>
            {
                MenuRowModel first = group.First();

                SubMenu[] subMenus = group
                    .Where(x => x.SubMenuSid.HasValue)
                    .OrderBy(x => x.SubMenuSeq ?? int.MaxValue)
                    .Select(x => new SubMenu
                    {
                        SubSid = x.SubMenuSid,
                        SubTitle = x.SubMenuName,
                        SubName = x.SubMenuName,
                        TypeGroup = MenuConstants.MenuType,
                        SubUrl = x.SubMenuUrl,
                        SubDesc = x.SubMenuDesc,
                        SubImgIcon = x.SubMenuImgIcon,
                        SubLv = 0,
                        SubParameter = x.SubMenuParameter,
                        SubProperty = MenuConstants.MenuType
                    })
                    .ToArray();

                Abstractions.Menu.Models.Menu menu = new Abstractions.Menu.Models.Menu
                {
                    Sid = first.MenuSid,
                    Title = first.MenuName,
                    TypeGroup = MenuConstants.MenuType,
                    Url = first.MenuUrl,
                    BackSid = first.ParentSid,
                    BackUrl = first.ParentUrl,
                    Desc = first.MenuDesc,
                    ImgIcon = first.MenuImgIcon,
                    Lv = 0,
                    ModuleName = string.Empty,
                    PageKind = string.Empty,
                    Parameter = first.MenuParameter,
                    Property = MenuConstants.MenuType,
                    Tiles = subMenus
                };

                return menu;
            })
            .OrderBy(x => x.BackSid)
            .ThenBy(x => x.Title)
            .ToList();

        return menus.ToArray();
    }

    /// <summary>
    /// 將查詢資料映射為 legacy Page[]
    /// </summary>
    /// <param name="rows">頁面資料列</param>
    /// <returns>legacy Page 陣列</returns>
    private static Page[] MapPages(List<PageRowModel> rows)
    {
        List<Page> pages = rows
            .OrderBy(x => x.MenuSid)
            .ThenBy(x => x.Seq)
            .Select(x => new Page
            {
                Sid = x.PageSid,
                Title = x.Title,
                Url = x.Url,
                Parameter = x.Parameter,
                Desc = x.Desc,
                Property = x.Desc,
                Seq = x.Seq,
                ImgIcon = x.ImgIcon,
                MenuSid = x.MenuSid,
                MenuName = x.MenuName,
                MenuUrl = x.MenuUrl 
            })
            .ToList();

        return pages.ToArray();
    }
}
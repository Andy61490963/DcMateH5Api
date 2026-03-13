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
    P.ADM_MENU_MODULE_SID AS ParentSid,
    P.URL AS ParentUrl,

    M.ADM_MENU_MODULE_SID AS MenuSid,
    M.ADM_MENU_MODULE_NAME AS MenuName,
    M.URL AS MenuUrl,
    M.PARAMETER AS MenuParameter,
    M.SEQ AS MenuSeq,
    M.[DESC] AS MenuDesc,
    M.IMGICON AS MenuImgIcon,

    S.ADM_MENU_MODULE_SID AS SubMenuSid,
    S.ADM_MENU_MODULE_NAME AS SubMenuName,
    S.URL AS SubMenuUrl,
    S.PARAMETER AS SubMenuParameter,
    S.SEQ AS SubMenuSeq,
    S.[DESC] AS SubMenuDesc,
    S.IMGICON AS SubMenuImgIcon

FROM ADM_MENU_MODULE M
LEFT JOIN ADM_MENU_MODULE S
    ON S.PARENT_ID = M.ADM_MENU_MODULE_SID
LEFT JOIN ADM_MENU_MODULE P
    ON M.PARENT_ID = P.ADM_MENU_MODULE_SID

ORDER BY
    M.PARENT_ID,
    M.SEQ,
    S.SEQ;";

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
                        SubSeq = x.SubMenuSeq,
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
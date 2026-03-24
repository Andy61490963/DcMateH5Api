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
        public const string EnableFlagYes = "Y";
    }

    private readonly IDbExecutor _db;

    public MenuService(IDbExecutor db)
    {
        _db = db;
    }

    /// <summary>
    /// 取得 legacy AuthInfo 格式的選單與頁面資料 (2026/03/13 Jack要求改為他制定的Json格式)
    /// 老闆邏輯：
    /// 1. Menu 全部顯示
    /// 2. Page 依使用者所屬群組授權過濾
    /// </summary>
    /// <param name="lv">頁面層級</param>
    /// <param name="userId">使用者識別</param>
    /// <returns>Legacy AuthInfo</returns>
    public async Task<AuthInfo> GetFullMenuByLvAsync(string lv, Guid userId)
    {
        int? pageLevel = ParsePageLevel(lv);

        List<MenuRowModel> menuRows = await GetAllMenuRowsAsync().ConfigureAwait(false);
        List<PageRowModel> pageRows = await GetAuthorizedPageRowsAsync(userId).ConfigureAwait(false);

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
    /// 取得全部主選單與子選單資料
    /// </summary>
    /// <returns>選單資料列</returns>
    private async Task<List<MenuRowModel>> GetAllMenuRowsAsync()
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
    /// 取得使用者有權限的頁面資料
    /// </summary>
    /// <param name="userId">使用者識別</param>
    /// <param name="pageLevel">頁面層級</param>
    /// <returns>頁面資料列</returns>
    private async Task<List<PageRowModel>> GetAuthorizedPageRowsAsync(Guid userId)
    {
        const string sql = @"
SELECT DISTINCT
    P.ADM_PAGE_MODULE_SID AS PageSid,
    P.TITLE AS Title,
    P.URL AS Url,
    P.PARAMETER AS Parameter,
    P.[DESC] AS [Desc],
    P.LV AS Lv,
    P.SEQ AS Seq,
    P.IMGICON AS ImgIcon,
    MPL.ADM_MENU_MODULE_SID AS MenuSid,
    M.ADM_MENU_MODULE_NAME AS MenuName,
    M.URL AS MenuUrl
FROM ADM_USERGROUP_USER_LIST GU
INNER JOIN ADM_USERGROUP G
    ON GU.GROUP_SID = G.GROUP_SID
INNER JOIN ADM_USERGROUP_PAGE_LIST GP
    ON GP.GROUP_SID = G.GROUP_SID
INNER JOIN ADM_PAGE_MODULE P
    ON P.ADM_PAGE_MODULE_SID = GP.ADM_PAGE_MODULE_SID
INNER JOIN ADM_MENU_PAGE_LINK MPL
    ON MPL.ADM_PAGE_MODULE_SID = P.ADM_PAGE_MODULE_SID
INNER JOIN ADM_MENU_MODULE M
    ON M.ADM_MENU_MODULE_SID = MPL.ADM_MENU_MODULE_SID
WHERE GU.USER_SID = @UserId
  AND G.ENABLE_FLAG = @EnableFlag
ORDER BY
    MPL.ADM_MENU_MODULE_SID,
    P.SEQ;";

        IEnumerable<PageRowModel> result = await _db
            .QueryAsync<PageRowModel>(
                sql,
                new
                {
                    UserId = userId,
                    EnableFlag = MenuConstants.EnableFlagYes,
                })
            .ConfigureAwait(false);

        return result.ToList();
    }

    /// <summary>
    /// 解析頁面層級
    /// </summary>
    /// <param name="lv">頁面層級字串</param>
    /// <returns>頁面層級，無法解析時回傳 null</returns>
    private static int? ParsePageLevel(string lv)
    {
        if (string.IsNullOrWhiteSpace(lv))
        {
            return null;
        }

        if (int.TryParse(lv, out int parsedLevel))
        {
            return parsedLevel;
        }

        return null;
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
                    .GroupBy(x => x.SubMenuSid)
                    .Select(subGroup => subGroup.First())
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
            .GroupBy(x => x.PageSid)
            .Select(group => group.First())
            .OrderBy(x => x.MenuSid)
            .ThenBy(x => x.Seq)
            .Select(x => new Page
            {
                Sid = x.PageSid,
                Title = x.Title,
                Url = x.Url,
                Parameter = x.Parameter,
                Desc = x.Desc,
                Property = MenuConstants.PageType,
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
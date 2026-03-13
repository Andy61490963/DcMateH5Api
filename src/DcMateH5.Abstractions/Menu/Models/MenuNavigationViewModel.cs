using System.Text.Json.Serialization;

namespace DcMateH5.Abstractions.Menu.Models;

/// <summary>
/// Legacy AuthInfo 回傳模型
/// </summary>
public class AuthInfo
{
    [JsonPropertyName("MmenuList")]
    public MenuList MmenuList { get; set; } = new MenuList();

    [JsonPropertyName("PageList")]
    public PageList PageList { get; set; } = new PageList();
}

/// <summary>
/// Legacy 主選單清單
/// </summary>
public class MenuList
{
    [JsonPropertyName("menuls")]
    public Menu[] Menuls { get; set; } = Array.Empty<Menu>();
}

/// <summary>
/// Legacy 頁面清單
/// </summary>
public class PageList
{
    [JsonPropertyName("pages")]
    public Page[] Pages { get; set; } = Array.Empty<Page>();
}

/// <summary>
/// Legacy 主選單模型
/// </summary>
public class Menu
{
    [JsonPropertyName("Sid")]
    public Guid Sid { get; set; }

    [JsonPropertyName("Title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("TypeGroup")]
    public string TypeGroup { get; set; } = string.Empty;

    [JsonPropertyName("Url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("BackSid")]
    public Guid? BackSid { get; set; }

    [JsonPropertyName("BackUrl")]
    public string BackUrl { get; set; } = string.Empty;

    [JsonPropertyName("Desc")]
    public string Desc { get; set; } = string.Empty;

    [JsonPropertyName("ImgIcon")]
    public string ImgIcon { get; set; } = string.Empty;

    [JsonPropertyName("Lv")]
    public int Lv { get; set; }

    [JsonPropertyName("ModuleName")]
    public string ModuleName { get; set; } = string.Empty;

    [JsonPropertyName("PageKind")]
    public string PageKind { get; set; } = string.Empty;

    [JsonPropertyName("Parameter")]
    public string Parameter { get; set; } = string.Empty;

    [JsonPropertyName("Property")]
    public string Property { get; set; } = string.Empty;

    [JsonPropertyName("tiles")]
    public SubMenu[] Tiles { get; set; } = Array.Empty<SubMenu>();
}

/// <summary>
/// Legacy 子選單模型
/// </summary>
public class SubMenu
{
    [JsonPropertyName("SubSid")]
    public Guid? SubSid { get; set; }

    [JsonPropertyName("SubTitle")]
    public string? SubTitle { get; set; } = string.Empty;

    [JsonPropertyName("SubName")]
    public string? SubName { get; set; } = string.Empty;

    [JsonPropertyName("TypeGroup")]
    public string TypeGroup { get; set; } = string.Empty;

    [JsonPropertyName("SubUrl")]
    public string? SubUrl { get; set; } = string.Empty;

    [JsonPropertyName("SubDesc")]
    public string? SubDesc { get; set; } = string.Empty;

    [JsonPropertyName("SubImgIcon")]
    public string? SubImgIcon { get; set; } = string.Empty;

    [JsonPropertyName("SubLv")]
    public int SubLv { get; set; }

    [JsonPropertyName("SubParameter")]
    public string? SubParameter { get; set; } = string.Empty;

    [JsonPropertyName("SubProperty")]
    public string SubProperty { get; set; } = string.Empty;
}

/// <summary>
/// Legacy 頁面模型
/// </summary>
public class Page
{
    [JsonPropertyName("Sid")]
    public Guid Sid { get; set; }

    [JsonPropertyName("Title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("Url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("Parameter")]
    public string? Parameter { get; set; } = string.Empty;

    [JsonPropertyName("Desc")]
    public string? Desc { get; set; } = string.Empty;

    [JsonPropertyName("Property")]
    public string? Property { get; set; } = string.Empty;

    [JsonPropertyName("Seq")]
    public int? Seq { get; set; }

    [JsonPropertyName("ImgIcon")]
    public string? ImgIcon { get; set; } = string.Empty;

    [JsonPropertyName("MENU_SID")]
    public Guid? MenuSid { get; set; }

    [JsonPropertyName("MENU_NAME")]
    public string MenuName { get; set; } = string.Empty;

    [JsonPropertyName("MENU_URL")]
    public string MenuUrl { get; set; } = string.Empty;
}

/// <summary>
/// 主選單 / 子選單查詢結果模型
/// </summary>
public class MenuRowModel
{
    public Guid? ParentSid { get; set; }

    public string? ParentUrl { get; set; }

    public Guid MenuSid { get; set; }

    public string MenuName { get; set; } = string.Empty;

    public string MenuUrl { get; set; } = string.Empty;

    public string? MenuParameter { get; set; }

    public int? MenuSeq { get; set; }

    public string? MenuDesc { get; set; }

    public string? MenuImgIcon { get; set; }

    public Guid? SubMenuSid { get; set; }

    public string? SubMenuName { get; set; }

    public string? SubMenuUrl { get; set; }

    public string? SubMenuParameter { get; set; }

    public int? SubMenuSeq { get; set; }

    public string? SubMenuDesc { get; set; }

    public string? SubMenuImgIcon { get; set; }
}

/// <summary>
/// 頁面查詢結果模型
/// </summary>
public class PageRowModel
{
    public Guid PageSid { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;

    public string? Parameter { get; set; }

    public string? Desc { get; set; }

    public int? Lv { get; set; }

    public int? Seq { get; set; }

    public string? ImgIcon { get; set; }

    public Guid MenuSid { get; set; }

    public string MenuName { get; set; } = string.Empty;

    public string MenuUrl { get; set; } = string.Empty;
}
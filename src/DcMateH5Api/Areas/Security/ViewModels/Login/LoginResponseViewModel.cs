using DcMateH5Api.Areas.Security.Models;
using DcMateH5.Abstractions.Menu.Models;

namespace DcMateH5Api.Areas.Security.ViewModels;

public class LoginResponseViewModel
{
    public Guid Sid { get; set; }
    
    public string User { get; set; } = string.Empty;
    public string LV { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;

    // 新增：起始時間
    public string ExpiresFrom { get; set; }
    public string ExpiresTo { get; set; }

    // --- 新增：存放選單樹狀結構的欄位 ---
    // 這裡的 MenuNode 是您在 Menu 模組定義的類別
    public AuthInfo Menus { get; set; } = new();

    public LoginResponseViewModel() { }

    public LoginResponseViewModel(UserAccount user, TokenResult? token)
    {
        this.User = user.Account;
        this.LV = user.LV?.ToString() ?? "0"; // 建議這裡改為動取抓取 user.LV，不要寫死 "2"
        this.Sid = user.Id;
        this.Token = token?.Token ?? "";
    }
}
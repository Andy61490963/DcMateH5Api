using DcMateH5Api.Areas.Security.Models;
using DcMateH5Api.Models;

namespace DcMateH5Api.Areas.Security.ViewModels;

public class LoginResponseViewModel
{
    // 1. 補齊你需要的欄位
    public string User { get; set; } = string.Empty;
    public string LV { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public string Sid { get; set; } = string.Empty;

    // 2. 新增：無參數構造函數 (解決 CS7036 報錯)
    public LoginResponseViewModel() { }

    // 3. 保留：原本同事寫的構造函數 (選配，若其他地方有用到就留著)
    public LoginResponseViewModel(UserAccount user, TokenResult? token)
    {
        this.User = user.Account;
        this.LV = "2";
        this.Sid = user.Id.ToString();
        this.Token = token?.Token ?? "";
    }
}
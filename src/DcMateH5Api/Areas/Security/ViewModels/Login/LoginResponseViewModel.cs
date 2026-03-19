using DcMateH5.Abstractions.Menu.Models;

namespace DcMateH5Api.Areas.Security.ViewModels.Login;

/// <summary>
/// Legacy 登入回傳模型
/// </summary>
public class LoginResponseViewModel
{
    /// <summary>
    /// Token 資訊
    /// </summary>
    public TokenInfo tokenInfo { get; set; } = new TokenInfo();

    /// <summary>
    /// 使用者資訊
    /// </summary>
    public UserInfo userInfo { get; set; } = new UserInfo();

    /// <summary>
    /// 權限 / 選單資訊
    /// </summary>
    public AuthInfo authInfo { get; set; } = new AuthInfo();
}

/// <summary>
/// Legacy Token 資訊
/// </summary>
public class TokenInfo
{
    public string TOKEN_STATUS { get; set; } = "false";

    public string ACCOUNT_NO { get; set; } = string.Empty;

    public string TOKEN_KEY { get; set; } = string.Empty;

    public DateTime? TOKEN_EXPIRY { get; set; }

    public int TOKEN_SEQ { get; set; }
}

/// <summary>
/// Legacy 使用者資訊
/// </summary>
public class UserInfo
{
    public string ACCOUNT_NO { get; set; } = string.Empty;

    public string NICKNAME { get; set; } = string.Empty;

    public string EMP_NO { get; set; } = string.Empty;

    public string DEPT_SID { get; set; } = string.Empty;

    public string TITLE_SID { get; set; } = string.Empty;

    public string SECURITY_ID { get; set; } = string.Empty;

    public string COMPANY { get; set; } = string.Empty;

    public string LV { get; set; } = string.Empty;

    public string REG_DATABASE { get; set; } = string.Empty;

    public string REG_EXPIRE_DATE { get; set; } = string.Empty;

    public string REG_CURR_USER_LIM { get; set; } = string.Empty;

    public string REG_CURR_USER { get; set; } = string.Empty;

    public string REG_COMPANY { get; set; } = string.Empty;

    public string REG_MSG { get; set; } = string.Empty;

    public string LOGIN_STATUS { get; set; } = "false";

    public string LOGIN_MSG { get; set; } = string.Empty;
}
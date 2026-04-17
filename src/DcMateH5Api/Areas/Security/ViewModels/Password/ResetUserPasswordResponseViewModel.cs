namespace DcMateH5Api.Areas.Security.ViewModels.Password;

/// <summary>
/// 重設其他帳號密碼結果
/// </summary>
public sealed record ResetUserPasswordResponseViewModel
{
    /// <summary>
    /// 被重設密碼的帳號
    /// </summary>
    public string Account { get; init; } = string.Empty;
}

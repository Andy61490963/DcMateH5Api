using System.ComponentModel.DataAnnotations;

namespace DcMateH5Api.Areas.Security.ViewModels.Password;

/// <summary>
/// 重設其他帳號密碼請求
/// </summary>
public sealed record ResetUserPasswordRequestViewModel
{
    /// <summary>
    /// 要被重設密碼的帳號
    /// </summary>
    [Required]
    public required string Account { get; init; }

    /// <summary>
    /// 新密碼
    /// </summary>
    [Required]
    public required string NewPassword { get; init; }
}

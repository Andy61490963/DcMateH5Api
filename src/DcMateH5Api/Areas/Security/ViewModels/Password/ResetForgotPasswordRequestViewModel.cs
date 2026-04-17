using System.ComponentModel.DataAnnotations;

namespace DcMateH5Api.Areas.Security.ViewModels.Password;

/// <summary>
/// Reset forgot password request.
/// </summary>
public sealed record ResetForgotPasswordRequestViewModel
{
    /// <summary>
    /// Password reset token from email.
    /// </summary>
    [Required]
    public required string Token { get; init; }

    /// <summary>
    /// New password.
    /// </summary>
    [Required]
    public required string NewPassword { get; init; }
}

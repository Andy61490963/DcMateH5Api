using System.ComponentModel.DataAnnotations;

namespace DcMateH5Api.Areas.Security.ViewModels.Password;

/// <summary>
/// Forgot password request.
/// </summary>
public sealed record ForgotPasswordRequestViewModel
{
    /// <summary>
    /// Account to reset.
    /// </summary>
    [Required]
    public required string Account { get; init; }
}

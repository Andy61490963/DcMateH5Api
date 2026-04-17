namespace DcMateH5Api.Areas.Security.ViewModels.Password;

/// <summary>
/// Forgot password response.
/// </summary>
public sealed record ForgotPasswordResponseViewModel
{
    /// <summary>
    /// Account that requested password reset.
    /// </summary>
    public string Account { get; init; } = string.Empty;

    /// <summary>
    /// Masked target email.
    /// </summary>
    public string Email { get; init; } = string.Empty;

    /// <summary>
    /// Reset token expiration time.
    /// </summary>
    public DateTime ExpiredTime { get; init; }
}

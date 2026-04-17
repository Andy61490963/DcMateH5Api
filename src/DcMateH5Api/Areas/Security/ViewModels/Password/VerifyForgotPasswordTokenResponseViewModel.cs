namespace DcMateH5Api.Areas.Security.ViewModels.Password;

/// <summary>
/// Password reset token verification response.
/// </summary>
public sealed record VerifyForgotPasswordTokenResponseViewModel
{
    /// <summary>
    /// Account bound to the token.
    /// </summary>
    public string Account { get; init; } = string.Empty;

    /// <summary>
    /// Token expiration time.
    /// </summary>
    public DateTime ExpiredTime { get; init; }
}

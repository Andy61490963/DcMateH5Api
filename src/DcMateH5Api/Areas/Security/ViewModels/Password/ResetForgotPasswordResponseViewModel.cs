namespace DcMateH5Api.Areas.Security.ViewModels.Password;

/// <summary>
/// Reset forgot password response.
/// </summary>
public sealed record ResetForgotPasswordResponseViewModel
{
    /// <summary>
    /// Account whose password was reset.
    /// </summary>
    public string Account { get; init; } = string.Empty;
}

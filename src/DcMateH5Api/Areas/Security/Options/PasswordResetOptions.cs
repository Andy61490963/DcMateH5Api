namespace DcMateH5Api.Areas.Security.Options;

/// <summary>
/// Password reset settings.
/// </summary>
public sealed class PasswordResetOptions
{
    public const string SectionName = "PasswordReset";

    public int TokenMinutes { get; init; } = 30;

    public string ResetUrl { get; init; } = string.Empty;
}

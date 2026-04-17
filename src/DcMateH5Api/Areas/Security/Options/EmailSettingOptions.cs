namespace DcMateH5Api.Areas.Security.Options;

/// <summary>
/// SMTP email settings.
/// </summary>
public sealed class EmailSettingOptions
{
    public const string SectionName = "EmailSetting";

    public string From { get; init; } = string.Empty;

    public string Sw { get; init; } = string.Empty;

    public int Port { get; init; } = 25;

    public string InternalSMTP { get; init; } = string.Empty;

    public string ExternalSMTP { get; init; } = string.Empty;

    public bool EnableSSL { get; init; }
}

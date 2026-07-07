namespace DcMateH5Api.BackgroundService;

public sealed class FormOrphanCleanupOptions
{
    public const string SectionName = "FormOrphanCleanup";

    public bool Enabled { get; init; } = true;
}

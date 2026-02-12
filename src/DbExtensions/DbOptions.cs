namespace DbExtensions;

public sealed class DbOptions
{
    public const string SectionName = "ConnectionStrings";
    public string Connection { get; init; } = string.Empty;
}
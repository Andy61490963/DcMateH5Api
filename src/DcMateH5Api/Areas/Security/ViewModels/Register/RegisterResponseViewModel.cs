namespace DcMateH5Api.Areas.Security.ViewModels.Register;

/// <summary>
/// Register response.
/// </summary>
public sealed record RegisterResponseViewModel
{
    /// <summary>
    /// User id.
    /// </summary>
    public Guid UserId { get; init; }

    /// <summary>
    /// Account.
    /// </summary>
    public string Account { get; init; } = string.Empty;

    /// <summary>
    /// User name.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Email address.
    /// </summary>
    public string? Email { get; init; }

    /// <summary>
    /// User level.
    /// </summary>
    public int Lv { get; init; }
}

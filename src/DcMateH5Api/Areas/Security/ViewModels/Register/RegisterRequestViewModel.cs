using System.ComponentModel.DataAnnotations;

namespace DcMateH5Api.Areas.Security.ViewModels.Register;

/// <summary>
/// Register request.
/// </summary>
public sealed record RegisterRequestViewModel
{
    /// <summary>
    /// Account.
    /// </summary>
    [Required]
    public required string Account { get; init; }

    /// <summary>
    /// Password.
    /// </summary>
    [Required]
    public required string Password { get; init; }

    /// <summary>
    /// Optional email address.
    /// </summary>
    [EmailAddress]
    public string? Email { get; init; }

    /// <summary>
    /// User level.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int Lv { get; init; }
}

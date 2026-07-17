using System.ComponentModel.DataAnnotations;

namespace DcMateH5.Abstractions.Prj.Models;

public sealed class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
}

public sealed class PrjProjectQuery
{
    [Range(1, int.MaxValue)] public int Page { get; init; } = 1;
    [Range(1, 100)] public int PageSize { get; init; } = 20;
    public string? Keyword { get; init; }
    public decimal? StatusNo { get; init; }
    public decimal? TypeNo { get; init; }
    public string? CustomerNo { get; init; }
    public bool? Enabled { get; init; }
    public DateTime? StartFrom { get; init; }
    public DateTime? StartTo { get; init; }
    public string SortBy { get; init; } = "Seq";
    public bool SortDescending { get; init; }
}

public sealed class PrjDetailQuery
{
    [Range(1, int.MaxValue)] public int Page { get; init; } = 1;
    [Range(1, 100)] public int PageSize { get; init; } = 20;
    public string? Keyword { get; init; }
    public decimal? StatusNo { get; init; }
    public decimal? ProcessTypeNo { get; init; }
    public string? UserAccount { get; init; }
    public bool? Enabled { get; init; }
    public DateTime? StartFrom { get; init; }
    public DateTime? EndTo { get; init; }
    public string SortBy { get; init; } = "Seq";
    public bool SortDescending { get; init; }
}

public class PrjProjectListItemDto
{
    public decimal ProjectSid { get; init; }
    public int? Seq { get; init; }
    public string ProjectCode { get; init; } = string.Empty;
    public string? ProjectName { get; init; }
    public decimal? StatusNo { get; init; }
    public string? StatusName { get; init; }
    public decimal? TypeNo { get; init; }
    public string? TypeName { get; init; }
    public string? CustomerNo { get; init; }
    public string? CustomerName { get; init; }
    public DateTime? StartTime { get; init; }
    public DateTime? ExpectedTime { get; init; }
    public DateTime? EndTime { get; init; }
    public string? IsOrder { get; init; }
    public bool Enabled { get; init; }
    public int DetailCount { get; init; }
    public int CompletedDetailCount { get; init; }
    public int OverdueDetailCount { get; init; }
    public DateTime? EditTime { get; init; }
}

public sealed class PrjProjectDto : PrjProjectListItemDto
{
    public string? CreateUser { get; init; }
    public DateTime? CreateTime { get; init; }
    public string? EditUser { get; init; }
}

public sealed class PrjDetailDto
{
    public decimal DetailSid { get; init; }
    public string ProjectCode { get; init; } = string.Empty;
    public decimal? ProcessTypeNo { get; init; }
    public string? ProcessTypeName { get; init; }
    public string? Summary { get; init; }
    public decimal StatusNo { get; init; }
    public string? StatusName { get; init; }
    public bool IsCompleted { get; init; }
    public bool IsOverdue { get; init; }
    public string? Comment { get; init; }
    public string? PrincipalUser { get; init; }
    public string? PrincipalUserName { get; init; }
    public string? SupportUser { get; init; }
    public string? SupportUserName { get; init; }
    public string? ReviewerUser { get; init; }
    public string? ReviewerUserName { get; init; }
    public DateTime? StartExpectedTime { get; init; }
    public DateTime? StartTime { get; init; }
    public DateTime? ExpectedTime { get; init; }
    public DateTime? EndTime { get; init; }
    public int? Seq { get; init; }
    public bool Enabled { get; init; }
    public string? FileName { get; init; }
    public string? CreateUser { get; init; }
    public DateTime? CreateTime { get; init; }
    public string? EditUser { get; init; }
    public DateTime? EditTime { get; init; }
}

public sealed class CreatePrjProjectRequest
{
    [Required, StringLength(150)] public string ProjectCode { get; init; } = string.Empty;
    [StringLength(255)] public string? ProjectName { get; init; }
    public decimal? StatusNo { get; init; }
    public decimal? TypeNo { get; init; }
    [StringLength(255)] public string? CustomerNo { get; init; }
    public DateTime? StartTime { get; init; }
    public DateTime? ExpectedTime { get; init; }
    public DateTime? EndTime { get; init; }
    [StringLength(255)] public string? IsOrder { get; init; }
    public int? Seq { get; init; }
}

public sealed class UpdatePrjProjectRequest
{
    [StringLength(255)] public string? ProjectName { get; init; }
    public decimal? StatusNo { get; init; }
    public decimal? TypeNo { get; init; }
    [StringLength(255)] public string? CustomerNo { get; init; }
    public DateTime? StartTime { get; init; }
    public DateTime? ExpectedTime { get; init; }
    public DateTime? EndTime { get; init; }
    [StringLength(255)] public string? IsOrder { get; init; }
    public int? Seq { get; init; }
    [Required] public DateTime? EditTime { get; init; }
}

public sealed class CreatePrjDetailRequest
{
    public decimal? ProcessTypeNo { get; init; }
    [StringLength(2000)] public string? Summary { get; init; }
    [Required] public decimal? StatusNo { get; init; }
    [StringLength(2000)] public string? Comment { get; init; }
    [StringLength(100)] public string? PrincipalUser { get; init; }
    [StringLength(255)] public string? SupportUser { get; init; }
    [StringLength(100)] public string? ReviewerUser { get; init; }
    public DateTime? StartExpectedTime { get; init; }
    public DateTime? StartTime { get; init; }
    public DateTime? ExpectedTime { get; init; }
    public DateTime? EndTime { get; init; }
    public int? Seq { get; init; }
    [StringLength(255)] public string? FileName { get; init; }
}

public sealed class UpdatePrjDetailRequest
{
    public decimal? ProcessTypeNo { get; init; }
    [StringLength(2000)] public string? Summary { get; init; }
    [Required] public decimal? StatusNo { get; init; }
    [StringLength(2000)] public string? Comment { get; init; }
    [StringLength(100)] public string? PrincipalUser { get; init; }
    [StringLength(255)] public string? SupportUser { get; init; }
    [StringLength(100)] public string? ReviewerUser { get; init; }
    public DateTime? StartExpectedTime { get; init; }
    public DateTime? StartTime { get; init; }
    public DateTime? ExpectedTime { get; init; }
    public DateTime? EndTime { get; init; }
    public int? Seq { get; init; }
    [StringLength(255)] public string? FileName { get; init; }
    [Required] public DateTime? EditTime { get; init; }
}

public sealed class ChangeEnabledRequest
{
    public bool Enabled { get; init; }
    [Required] public DateTime? EditTime { get; init; }
}

public sealed class ChangePrjDetailStatusRequest
{
    [Required] public decimal? StatusNo { get; init; }
    [Required] public DateTime? EditTime { get; init; }
}

public sealed class ReorderPrjProjectsRequest
{
    [Required, MinLength(1)] public IReadOnlyList<PrjProjectOrderItem> Items { get; init; } = Array.Empty<PrjProjectOrderItem>();
}

public sealed class PrjProjectOrderItem
{
    [Required] public string ProjectCode { get; init; } = string.Empty;
    public int Seq { get; init; }
    [Required] public DateTime? EditTime { get; init; }
}

public sealed class ReorderPrjDetailsRequest
{
    [Required, MinLength(1)] public IReadOnlyList<PrjDetailOrderItem> Items { get; init; } = Array.Empty<PrjDetailOrderItem>();
}

public sealed class PrjDetailOrderItem
{
    public decimal DetailSid { get; init; }
    public int Seq { get; init; }
    [Required] public DateTime? EditTime { get; init; }
}

public sealed class PrjOptionDto
{
    public decimal Value { get; init; }
    public string Text { get; init; } = string.Empty;
    public bool? IsCompleted { get; init; }
}

public sealed class PrjLookupOptionsDto
{
    public IReadOnlyList<PrjOptionDto> ProjectStatuses { get; init; } = Array.Empty<PrjOptionDto>();
    public IReadOnlyList<PrjOptionDto> ProjectTypes { get; init; } = Array.Empty<PrjOptionDto>();
    public IReadOnlyList<PrjOptionDto> DetailStatuses { get; init; } = Array.Empty<PrjOptionDto>();
    public IReadOnlyList<PrjOptionDto> ProcessTypes { get; init; } = Array.Empty<PrjOptionDto>();
}

public sealed class PrjTextOptionDto
{
    public string Value { get; init; } = string.Empty;
    public string Text { get; init; } = string.Empty;
}

public enum PrjErrorCode
{
    BadRequest,
    Unauthorized,
    Forbidden,
    NotFound,
    Conflict,
    UnhandledException
}

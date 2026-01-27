namespace DcMateH5Api.Areas.Form.Models.Excel;

public sealed class ExportFileResult
{
    public required string FileName { get; init; }
    public required string ContentType { get; init; }
    public required byte[] Content { get; init; }
}
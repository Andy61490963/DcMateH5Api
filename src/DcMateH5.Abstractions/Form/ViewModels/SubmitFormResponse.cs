namespace DcMateH5.Abstractions.Form.ViewModels;

public sealed class SubmitFormResponse
{
    public required string RowId { get; init; }
    public required bool IsInsert { get; init; }
}

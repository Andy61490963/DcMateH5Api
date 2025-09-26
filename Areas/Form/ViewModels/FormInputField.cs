namespace DcMateH5Api.Areas.Form.ViewModels;

/// <summary>
/// Represent single field input for submission.
/// </summary>
public class FormInputField
{
    public Guid FieldConfigId { get; set; }

    /// <summary>
    /// 資料庫欄位名稱，供前端識別欄位用途（例如關聯鍵）。
    /// </summary>
    public string? ColumnName { get; set; }

    public string? Value { get; set; }
}


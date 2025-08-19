namespace DynamicForm.Areas.Form.ViewModels;

/// <summary>
/// 描述 View 欄位的來源表資訊
/// </summary>
public class ViewColumnSource
{
    public string COLUMN_NAME { get; set; } = string.Empty;
    public string? SOURCE_TABLE { get; set; }
}

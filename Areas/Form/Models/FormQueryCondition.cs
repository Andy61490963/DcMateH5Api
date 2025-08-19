using ClassLibrary;

namespace DynamicForm.Areas.Form.Models;

/// <summary>
/// 描述查詢條件的資料模型。
/// </summary>
public class FormQueryCondition
{
    /// <summary>要比對的欄位名稱。</summary>
    public string Column { get; set; } = string.Empty;
    
    /// <summary>
    /// 查詢元件類型，可由此自動推斷 ConditionType />。
    /// </summary>
    public QueryConditionType? QueryConditionType { get; set; }
        = null;

    /// <summary>主要的比對值。</summary>
    public string? Value { get; set; }
        = string.Empty;

    /// <summary>區間比對的第二個值。</summary>
    public string? Value2 { get; set; }
        = string.Empty;

    /// <summary>欄位的 SQL 資料型別，用於轉型。</summary>
    public string DataType { get; set; } = string.Empty;
}

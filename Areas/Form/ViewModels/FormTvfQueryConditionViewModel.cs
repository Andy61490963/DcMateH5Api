using ClassLibrary;

namespace DcMateH5Api.Areas.Form.Models;

public sealed record FormTvfSearchRequest(
    Guid FormMasterId,
    int Page = 1,
    int PageSize = 20,
    List<FormTvfQueryConditionViewModel>? Conditions = null,
    List<FormOrderBy>? OrderBys = null,
    Dictionary<string, object?>? TvfParameters = null
);


/// <summary>
/// 描述查詢條件的資料模型。
/// </summary>
public class FormTvfQueryConditionViewModel
{
    /// <summary>要比對的欄位名稱。</summary>
    public string Column { get; set; } = string.Empty;

    /// <summary>
    /// 條件運算子類型（可選，用於覆寫預設映射）
    /// </summary>
    public ConditionType? ConditionType { get; set; }
    
    /// <summary>主要的比對值。</summary>
    public string? Value { get; set; }
        = string.Empty;

    /// <summary>區間比對的第二個值。</summary>
    public string? Value2 { get; set; }
        = string.Empty;

    /// <summary>
    /// 多個值（用於 IN 查詢）
    /// </summary>
    public List<object>? Values { get; set; }
    
    /// <summary>欄位的 SQL 資料型別，用於轉型。</summary>
    public string DataType { get; set; } = string.Empty;
}
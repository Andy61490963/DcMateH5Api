using ClassLibrary;

namespace DcMateH5Api.Areas.Form.Models;

public sealed record FormSearchRequest(
    Guid FormMasterId,
    List<FormQueryConditionViewModel>? Conditions = null,
    List<FormOrderBy>? OrderBys = null
);

/// <summary>
/// 描述查詢條件的資料模型。
/// </summary>
public class FormQueryConditionViewModel
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

public class MappingListQuery
{
    public string BaseId { get; set; } = string.Empty;
    
    public MappingListType? Type { get; set; }
    
    public Dictionary<string, string> Filters { get; set; }
        = new(StringComparer.OrdinalIgnoreCase);
}

public sealed record FormOrderBy(
    string Column,
    SortType Direction = SortType.Asc
);

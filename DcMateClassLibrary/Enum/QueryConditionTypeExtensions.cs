using ClassLibrary;

namespace ClassLibrary;

/// <summary>
/// 提供 <see cref="QueryConditionType"/> 與 <see cref="ConditionType"/> 之間的轉換。
/// </summary>
public static class QueryConditionTypeExtensions
{
    /// <summary>
    /// 將查詢元件類型對應到 SQL 運算子類型。
    /// </summary>
    /// <param name="type">介面上的查詢元件類型。</param>
    /// <returns>對應的運算子類型。</returns>
    public static ConditionType ToConditionType(this QueryComponentType type) => type switch
    {
        // 文字輸入通常做模糊搜尋
        QueryComponentType.Text => ConditionType.Like,
        
        // 數字與日期多半用於區間比對
        QueryComponentType.Number => ConditionType.Between,
        QueryComponentType.Date => ConditionType.Between,
        
        // 數值比較預設為大於等於
        QueryComponentType.NumberComparison => ConditionType.GreaterThanOrEqual,
        
        // 日期比較預設為大於等於
        QueryComponentType.DateComparison => ConditionType.GreaterThanOrEqual,
        
        // 單選下拉與未指定則採等於比較
        _ => ConditionType.Equal
    };
}

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
    public static ConditionType ToConditionType(this QueryConditionType type) => type switch
    {
        // 文字輸入通常做模糊搜尋
        QueryConditionType.Text => ConditionType.Like,
        // 數字與日期多半用於區間比對，若前端只提供單一值仍可重用 Between 的邏輯
        QueryConditionType.Number => ConditionType.Between,
        QueryConditionType.Date => ConditionType.Between,
        // 下拉與未指定則採等於比較
        _ => ConditionType.Equal
    };
}

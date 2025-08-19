namespace ClassLibrary;

/// <summary>
/// 查詢條件的運算子類型。
/// </summary>
public enum ConditionType
{
    /// <summary>等於</summary>
    Equal,
    /// <summary>模糊比對</summary>
    Like,
    /// <summary>區間比對</summary>
    Between
}

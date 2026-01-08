using ClassLibrary;

namespace DcMateH5Api.Helper;

/// <summary>
/// FormFieldHelper
///
/// 核心目的：
/// **解決「SQL 欄位資料型別」與「前端 UI 控制元件」之間的對應關係問題**
///
/// 在動態表單 / Metadata 驅動系統中：
/// - 欄位來源是資料庫（schema / view / 設定表）
/// - 但實際呈現給使用者的是「表單控制元件」或「查詢條件元件」
///
/// 本 Helper 負責的事情：
/// 1. 根據 SQL 資料型別，限制「允許使用的 UI 元件」
/// 2. 避免錯誤組合（例如：bit 欄位用 Textarea、datetime 用 Checkbox）
/// 3. 提供預設控制元件，讓表單在「沒特別設定」時仍可正常運作
///
/// 設計原則：
/// - 所有「型別對應規則」集中管理
/// - 不讓判斷邏輯散落在 Controller / Service / View
/// - 讓表單行為可預測、可維護、可擴充
/// </summary>
public static class FormFieldHelper
{
    /// <summary>
    /// SQL 資料型別 → 允許使用的「表單控制元件」白名單
    ///
    /// 用途：
    /// - 限制某種 SQL 型別「可以搭配哪些 UI 控制元件」
    ///
    /// 為什麼要白名單？
    /// - 避免錯誤設定（例如 bit 卻選 Textarea）
    /// - 確保資料輸入型態與 DB 欄位語意一致
    ///
    /// 使用情境：
    /// - 表單設計器（Form Designer）
    /// - 動態欄位設定畫面
    /// - 預設控制元件推斷
    /// </summary>
    private static readonly Dictionary<SqlDataType, List<FormControlType>> ControlTypeWhitelistMap = new()
    {
        { SqlDataType.DateTime, new() { FormControlType.Date } },
        { SqlDataType.Bit,      new() { FormControlType.Checkbox } },

        // 數值型欄位，允許數字輸入、文字輸入、下拉選單（如代碼表）
        { SqlDataType.Int,      new() { FormControlType.Number, FormControlType.Text, FormControlType.Dropdown } },
        { SqlDataType.Decimal,  new() { FormControlType.Number, FormControlType.Text } },

        // 字串型欄位，通常最彈性
        { SqlDataType.NVarChar, new() { FormControlType.Number, FormControlType.Text, FormControlType.Dropdown, FormControlType.Checkbox } },
        { SqlDataType.VarChar,  new() { FormControlType.Number, FormControlType.Text, FormControlType.Dropdown, FormControlType.Checkbox } },

        // 長文字欄位
        { SqlDataType.Text,     new() { FormControlType.Textarea, FormControlType.Text } },

        // 無法辨識型別時的保守預設
        { SqlDataType.Unknown,  new() { FormControlType.Text } }
    };

    /// <summary>
    /// SQL 資料型別 → 允許使用的「查詢條件元件」白名單
    ///
    /// 用途：
    /// - 限制搜尋 / 篩選畫面中，可使用的查詢條件元件
    ///
    /// 與表單控制元件不同點：
    /// - 查詢條件偏「搜尋體驗」
    /// - 不一定要與實際輸入控制元件 1:1 相同
    ///
    /// 使用情境：
    /// - List / Grid 查詢條件
    /// - 動態搜尋列
    /// </summary>
    private static readonly Dictionary<SqlDataType, List<QueryComponentType>> QueryConditionTypeWhitelistMap = new()
    {
        { SqlDataType.DateTime, new() { QueryComponentType.Date } },
        { SqlDataType.Bit,      new() { QueryComponentType.Dropdown } },

        { SqlDataType.Int,      new() { QueryComponentType.Number, QueryComponentType.Text, QueryComponentType.Dropdown } },
        { SqlDataType.Decimal,  new() { QueryComponentType.Number, QueryComponentType.Text } },

        { SqlDataType.NVarChar, new() { QueryComponentType.Number, QueryComponentType.Text, QueryComponentType.Dropdown } },
        { SqlDataType.VarChar,  new() { QueryComponentType.Number, QueryComponentType.Text, QueryComponentType.Dropdown } },

        { SqlDataType.Text,     new() { QueryComponentType.Text } },
        { SqlDataType.Unknown,  new() { QueryComponentType.Text } }
    };

    /// <summary>
    /// 取得指定 SQL 型別「允許使用的表單控制元件清單」
    ///
    /// 用途：
    /// - 驗證表單設定是否合法
    /// - 產生控制元件選項下拉清單
    /// </summary>
    /// <param name="dataType">SQL 資料型別字串（通常來自 schema 或設定表）</param>
    public static List<FormControlType> GetControlTypeWhitelist(string dataType)
    {
        var sqlType = ParseSqlDataType(dataType);

        return ControlTypeWhitelistMap.TryGetValue(sqlType, out var list)
            ? list
            : ControlTypeWhitelistMap[SqlDataType.Unknown];
    }

    /// <summary>
    /// 取得指定 SQL 型別「允許使用的查詢條件元件清單」
    ///
    /// 用途：
    /// - 動態產生查詢條件 UI
    /// - 防止錯誤型別的搜尋元件被選用
    /// </summary>
    /// <param name="dataType">SQL 資料型別字串（通常來自 schema 或設定表）</param>
    public static List<QueryComponentType> GetQueryConditionTypeWhitelist(string dataType)
    {
        var sqlType = ParseSqlDataType(dataType);

        return QueryConditionTypeWhitelistMap.TryGetValue(sqlType, out var list)
            ? list
            : QueryConditionTypeWhitelistMap[SqlDataType.Unknown];
    }

    /// <summary>
    /// 取得指定 SQL 型別的「預設表單控制元件」
    ///
    /// 設計目的：
    /// - 當欄位尚未指定控制元件時
    /// - 提供一個「合理且安全」的預設值
    /// </summary>
    public static FormControlType GetDefaultControlType(string dataType)
    {
        var whitelist = GetControlTypeWhitelist(dataType);

        // 預設取白名單中的第一個，代表最建議的控制元件
        return whitelist.FirstOrDefault();
    }

    /// <summary>
    /// 將 SQL Server 的資料型別字串，轉換為系統內部使用的 Enum
    ///
    /// 用途：
    /// - 統一 SQL 型別判斷邏輯
    /// - 避免系統各處使用 magic string（"int" / "varchar"…）
    ///
    /// 設計重點：
    /// - 安全解析
    /// - 無法識別時回傳 Unknown（保守策略）
    /// </summary>
    public static SqlDataType ParseSqlDataType(string dataType)
    {
        return dataType?.ToLowerInvariant() switch
        {
            "int" => SqlDataType.Int,
            "decimal" => SqlDataType.Decimal,
            "bit" => SqlDataType.Bit,
            "nvarchar" => SqlDataType.NVarChar,
            "varchar" => SqlDataType.VarChar,
            "datetime" => SqlDataType.DateTime,
            "text" => SqlDataType.Text,
            _ => SqlDataType.Unknown
        };
    }
}

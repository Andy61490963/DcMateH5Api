using System.Collections.Generic;
using System.Linq;
using ClassLibrary;

namespace DcMateH5Api.Helper;

/// <summary>
/// 提供欄位對應的控制元件型別、預設寬度與邏輯的輔助方法。
/// </summary>
public static class FormFieldHelper
{
    /// <summary>
    /// 各 SQL 資料型別對應允許使用的控制元件清單。
    /// </summary>
    private static readonly Dictionary<SqlDataType, List<FormControlType>> ControlTypeWhitelistMap = new()
    {
        { SqlDataType.DateTime, new() { FormControlType.Date } },
        { SqlDataType.Bit,      new() { FormControlType.Checkbox } },
        { SqlDataType.Int,      new() { FormControlType.Number, FormControlType.Text, FormControlType.Dropdown } },
        { SqlDataType.Decimal,  new() { FormControlType.Number, FormControlType.Text } },
        { SqlDataType.NVarChar, new() { FormControlType.Number, FormControlType.Text, FormControlType.Dropdown } },
        { SqlDataType.VarChar,  new() { FormControlType.Number, FormControlType.Text, FormControlType.Dropdown } },
        { SqlDataType.Text,     new() { FormControlType.Textarea, FormControlType.Text } },
        { SqlDataType.Unknown,  new() { FormControlType.Text } }
    };

    /// <summary>
    /// 各 SQL 資料型別對應允許使用的查詢條件元件清單。
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
    /// 取得允許的控制元件清單。
    /// </summary>
    /// <param name="dataType">SQL 資料型別字串（來源為 schema）</param>
    public static List<FormControlType> GetControlTypeWhitelist(string dataType)
    {
        var sqlType = ParseSqlDataType(dataType);
        return ControlTypeWhitelistMap.TryGetValue(sqlType, out var list)
            ? list
            : ControlTypeWhitelistMap[SqlDataType.Unknown];
    }

    /// <summary>
    /// 取得允許的查詢條件元件清單。
    /// </summary>
    /// <param name="dataType">SQL 資料型別字串（來源為 schema）</param>
    public static List<QueryComponentType> GetQueryConditionTypeWhitelist(string dataType)
    {
        var sqlType = ParseSqlDataType(dataType);
        return QueryConditionTypeWhitelistMap.TryGetValue(sqlType, out var list)
            ? list
            : QueryConditionTypeWhitelistMap[SqlDataType.Unknown];
    }

    /// <summary>
    /// 取得預設控制元件型別。
    /// </summary>
    public static FormControlType GetDefaultControlType(string dataType)
    {
        var whitelist = GetControlTypeWhitelist(dataType);
        return whitelist.FirstOrDefault();
    }

    /// <summary>
    /// 將 SQL 資料型別字串轉換為 Enum（安全解析）
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

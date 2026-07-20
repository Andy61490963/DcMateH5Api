using System.Globalization;

namespace DcMateClassLibrary.Helper.FormHelper;

/// <summary>
/// SQL 型別轉換集中 Helper
///
/// 核心用途：
/// **解決動態系統中「欄位與主鍵（ID / PK）型別不固定」的問題**
///
/// 在實務上：
/// - API / 前端 傳進來的值幾乎永遠是 string / object
/// - 但資料庫實際欄位型別可能是：
///   int / bigint / decimal / datetime / bit / uniqueidentifier
/// - 尤其是「主鍵（ID）」可能同時存在 GUID、INT、DECIMAL、VARCHAR 等不同設計
///
/// 本 Helper 的責任：
/// 1. 根據「SQL 欄位型別」轉成 DB 可接受的 .NET 型別
/// 2. 區分「一般欄位」與「主鍵（PK）」的轉型策略
/// 3. 在寫入 DB（Dapper）前，避免型別錯誤導致 SQL 例外
///
/// 設計重點：
/// - 一般欄位：允許轉型失敗（回傳 null）
/// - 主鍵（PK）：轉型失敗即視為系統錯誤，直接 throw（Fail-Fast）
///
/// 好處：
/// - 避免 Controller / Service 散落一堆 Convert / Parse
/// - 統一處理 GUID / INT / DECIMAL 等 ID 型別差異
/// - 可集中維護與擴充 SQL Server 型別支援
/// </summary>
public static class ConvertToColumnTypeHelper
{
    /// <summary>
    /// 嚴格依 SQL Server 欄位型別轉換不可信任的輸入。
    /// 轉換失敗時回傳 false，避免把錯誤值誤當成 NULL 或 false 寫入資料庫。
    /// </summary>
    public static bool TryConvertStrict(string? sqlType, object? value, out object? convertedValue)
    {
        convertedValue = null;

        if (value is null)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(sqlType))
        {
            return false;
        }

        var normalizedSqlType = sqlType
            .Trim()
            .Split('(', 2)[0]
            .Trim()
            .ToLowerInvariant();
        var str = System.Convert.ToString(value, CultureInfo.InvariantCulture)?.Trim();

        switch (normalizedSqlType)
        {
            case "tinyint":
                if (byte.TryParse(str, NumberStyles.Integer, CultureInfo.InvariantCulture, out var tinyIntValue))
                {
                    convertedValue = tinyIntValue;
                    return true;
                }
                return false;

            case "smallint":
                if (short.TryParse(str, NumberStyles.Integer, CultureInfo.InvariantCulture, out var smallIntValue))
                {
                    convertedValue = smallIntValue;
                    return true;
                }
                return false;

            case "int":
                if (int.TryParse(str, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue))
                {
                    convertedValue = intValue;
                    return true;
                }
                return false;

            case "bigint":
                if (long.TryParse(str, NumberStyles.Integer, CultureInfo.InvariantCulture, out var longValue))
                {
                    convertedValue = longValue;
                    return true;
                }
                return false;

            case "decimal":
            case "numeric":
            case "money":
            case "smallmoney":
                if (decimal.TryParse(str, NumberStyles.Number, CultureInfo.InvariantCulture, out var decimalValue))
                {
                    convertedValue = decimalValue;
                    return true;
                }
                return false;

            case "float":
                if (double.TryParse(str, NumberStyles.Float, CultureInfo.InvariantCulture, out var doubleValue))
                {
                    convertedValue = doubleValue;
                    return true;
                }
                return false;

            case "real":
                if (float.TryParse(str, NumberStyles.Float, CultureInfo.InvariantCulture, out var floatValue))
                {
                    convertedValue = floatValue;
                    return true;
                }
                return false;

            case "bit":
                if (value is bool boolValue)
                {
                    convertedValue = boolValue;
                    return true;
                }

                if (string.Equals(str, "1", StringComparison.Ordinal) ||
                    string.Equals(str, "true", StringComparison.OrdinalIgnoreCase))
                {
                    convertedValue = true;
                    return true;
                }

                if (string.Equals(str, "0", StringComparison.Ordinal) ||
                    string.Equals(str, "false", StringComparison.OrdinalIgnoreCase))
                {
                    convertedValue = false;
                    return true;
                }
                return false;

            case "date":
            case "datetime":
            case "datetime2":
            case "smalldatetime":
                if (value is DateTime dateTimeValue)
                {
                    convertedValue = dateTimeValue;
                    return true;
                }

                if (DateTime.TryParse(
                        str,
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.RoundtripKind,
                        out var parsedDateTime))
                {
                    convertedValue = parsedDateTime;
                    return true;
                }
                return false;

            case "datetimeoffset":
                if (value is DateTimeOffset dateTimeOffsetValue)
                {
                    convertedValue = dateTimeOffsetValue;
                    return true;
                }

                if (DateTimeOffset.TryParse(
                        str,
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.AllowWhiteSpaces,
                        out var parsedDateTimeOffset))
                {
                    convertedValue = parsedDateTimeOffset;
                    return true;
                }
                return false;

            case "time":
                if (value is TimeSpan timeSpanValue)
                {
                    convertedValue = timeSpanValue;
                    return true;
                }

                if (TimeSpan.TryParse(str, CultureInfo.InvariantCulture, out var parsedTimeSpan))
                {
                    convertedValue = parsedTimeSpan;
                    return true;
                }
                return false;

            case "uniqueidentifier":
                if (value is Guid guidValue)
                {
                    convertedValue = guidValue;
                    return true;
                }

                if (Guid.TryParse(str, out var parsedGuid))
                {
                    convertedValue = parsedGuid;
                    return true;
                }
                return false;

            case "nvarchar":
            case "varchar":
            case "nchar":
            case "char":
            case "text":
            case "ntext":
            case "xml":
                convertedValue = System.Convert.ToString(value, CultureInfo.InvariantCulture);
                return true;

            default:
                return false;
        }
    }

    /// <summary>
    /// 一般欄位用的型別轉換方法
    ///
    /// 用途：
    /// - 將「來源不可信的 object / string」
    /// - 依據「SQL 欄位型別」轉成對應的 .NET 型別
    ///
    /// 使用情境：
    /// - 動態表單欄位
    /// - 使用者輸入資料
    /// - 非主鍵欄位
    ///
    /// 特性：
    /// - 轉型失敗時回傳 null（不 throw）
    /// - 避免單一欄位錯誤導致整筆資料無法寫入
    ///
    /// 常見搭配：
    /// - 動態 Insert / Update SQL
    /// - Dapper Parameter 綁定
    /// </summary>
    public static object? Convert(string? sqlType, object? value)
    {
        // DB 欄位允許 null，直接回 null（避免多餘轉型）
        if (value is null) return null;

        // 所有型別一律先轉 string，統一入口
        var str = value.ToString();

        // 若沒有指定 SQL 型別，直接原樣回傳（保守策略）
        if (string.IsNullOrWhiteSpace(sqlType)) return value;

        // SQL Server 型別判斷（忽略大小寫）
        switch (sqlType.ToLower())
        {
            case "int":
            case "bigint":
                return long.TryParse(str, out var l) ? l : null;

            case "decimal":
            case "numeric":
                return decimal.TryParse(str, out var d) ? d : null;

            case "bit":
                return str == "1" || string.Equals(str, "true", StringComparison.OrdinalIgnoreCase);

            case "datetime":
            case "smalldatetime":
            case "date":
                return DateTime.TryParse(str, out var dt) ? dt : null;

            case "nvarchar":
            case "varchar":
            case "nchar":
            case "char":
            case "text":
                return str;

            default:
                return str;
        }
    }

    /// <summary>
    /// 主鍵（Primary Key / ID）專用的型別轉換方法
    ///
    /// 目的：
    /// - 解決「ID 型別不固定（GUID / INT / DECIMAL / VARCHAR）」的問題
    ///
    /// 設計原則：
    /// - PK 屬於系統級關鍵資料
    /// - 轉型錯誤通常代表資料異常或流程錯誤
    /// - 因此採用 Fail-Fast 策略，轉型失敗直接 throw
    ///
    /// 與 Convert() 的差異：
    /// - Convert()：一般欄位，錯誤可容忍
    /// - ConvertPkType()：主鍵資料，錯誤不可容忍
    /// </summary>
    public static object ConvertPkType(string? id, string pkType)
    {
        // PK 不允許 null，直接擋
        if (id == null)
            throw new ArgumentNullException(nameof(id));

        switch (pkType.ToLower())
        {
            case "uniqueidentifier":
                return Guid.Parse(id);

            case "decimal":
            case "numeric":
                return System.Convert.ToDecimal(id);

            case "bigint":
                return System.Convert.ToInt64(id);

            case "int":
                return System.Convert.ToInt32(id);

            case "nvarchar":
            case "varchar":
            case "char":
                return id;

            default:
                throw new NotSupportedException($"不支援的型別: {pkType}");
        }
    }
}

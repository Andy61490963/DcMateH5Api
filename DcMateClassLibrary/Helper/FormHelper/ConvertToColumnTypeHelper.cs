namespace DcMateH5Api.Helper;

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
                return str == "1" || str.Equals("true", StringComparison.OrdinalIgnoreCase);

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

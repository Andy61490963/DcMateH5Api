namespace DcMateH5Api.Helper;

/// <summary>
/// GeneratePkValueHelper
///
/// 核心用途：
/// **依據資料庫主鍵（PK）型別，產生對應且合法的主鍵值**
///
/// 使用背景：
/// - 動態表單 / 動態資料表系統
/// - 主鍵型別不固定（GUID / INT / BIGINT / DECIMAL / VARCHAR）
/// - 新增資料時，系統必須「主動產生 PK」
///
/// 為什麼需要這支 Helper？
/// - API / Service 層不知道實際 PK 型別
/// - 不希望在各處 if/else 判斷怎麼產生 ID
/// - 統一管理 PK 生成策略，避免邏輯分散
///
/// 設計原則：
/// - PK 屬於系統級資料，不可為 null
/// - 每種 SQL 型別對應一種「合理的 PK 生成方式」
/// - 不支援的型別直接 fail-fast
/// </summary>
public class GeneratePkValueHelper
{
    /// <summary>
    /// 根據指定的主鍵型別，產生對應的 PK 值
    /// </summary>
    /// <param name="pkType">
    /// SQL Server 主鍵型別字串
    /// （例如：uniqueidentifier / int / bigint / decimal / varchar）
    /// </param>
    /// <returns>符合該 PK 型別的主鍵值</returns>
    public static object GeneratePkValue(string pkType)
    {
        switch (pkType.ToLower())
        {
            case "uniqueidentifier":
                // GUID 型主鍵：直接產生新的 GUID
                return Guid.NewGuid();

            case "decimal":
            case "numeric":
                // Decimal 型主鍵：
                // 兼容於舊系統或特殊流水號設計
                return RandomHelper.GenerateRandomDecimal();

            case "bigint":
                // BigInt 型主鍵：
                // 使用 Snowflake ID，確保分散式下唯一
                return RandomHelper.NextSnowflakeId();

            case "int":
                // Int 型主鍵：
                // 將 Snowflake ID 轉為 int（可能溢位，需確認實務風險）
                return unchecked((int)RandomHelper.NextSnowflakeId());

            case "nvarchar":
            case "varchar":
            case "char":
                // 字串型主鍵：
                // 使用不含 '-' 的 GUID，長度穩定、碰撞機率極低
                return Guid.NewGuid().ToString("N");

            default:
                // 明確不支援的主鍵型別，直接 fail-fast
                throw new NotSupportedException($"不支援的主鍵型別: {pkType}");
        }
    }
}

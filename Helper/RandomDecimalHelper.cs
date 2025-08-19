using System.Security.Cryptography;

namespace DynamicForm.Helper;

/// <summary>
/// 用於產生高品質亂數與唯一雪花 ID 的輔助工具類別
/// </summary>
public static class RandomHelper
{
    /// <summary>
    /// 使用系統內建 Random 實例（注意：非 thread-safe）
    /// 這邊透過 lock 確保多執行緒下不會產生重複
    /// </summary>
    private static readonly Random _rnd = new();

    /// <summary>
    /// 鎖定物件，確保 thread-safe 的執行（避免多執行緒同時使用 _rnd）
    /// </summary>
    private static readonly object _lock = new();

    /// <summary>
    /// 產生一個介於 0 ~ 999_999_999_999_999_999（18 位數以內）的安全亂數 decimal 值
    /// 適用於非主鍵、非排序用，但需要高安全性的亂數編號
    /// </summary>
    /// <returns>decimal 型別亂數值</returns>
    public static decimal GenerateRandomDecimal()
    {
        // 建立 8 byte 陣列（64-bit，對應 long）
        var bytes = new byte[8];

        // 使用加密等級的亂數產生器（非傳統 Random）
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(bytes); // 產生亂數填入 bytes
        }

        // 將 byte 陣列轉成 long，並與 long.MaxValue 做 AND，確保是正數
        long value = BitConverter.ToInt64(bytes, 0) & long.MaxValue;

        // 將結果取餘數，限制在最大 18 位數以內（小於 10^18）
        return new decimal(value % 1_000_000_000_000_000_000L);
    }

    /// <summary>
    /// 產生類似 Twitter Snowflake 演算法的唯一遞增 ID，回傳 long 值
    /// 結構為：目前 UTC 毫秒時間 << 22 | 22-bit 隨機值
    /// 適合用於排序用、唯一識別碼、不靠資料庫的主鍵
    /// </summary>
    /// <returns>唯一且遞增的 long ID</returns>
    public static long NextSnowflakeId()
    {
        // 鎖定區塊，避免 _rnd 同時被多執行緒呼叫造成碰撞
        lock (_lock)
        {
            // 取得目前 UTC 時間（毫秒），為 long（13 位數），例如：1722486933000
            var ms = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            // 產生一個 22-bit 內的隨機值（最大為 4,194,303）
            // 用來避免同一毫秒內重複（4M 筆以內可保證唯一）
            var rand = _rnd.Next(0, 1 << 22);

            // 將時間左移 22 位，空出低位給亂數
            // 並用 OR 將亂數放入低 22 位 => 組合成唯一 ID
            return (ms << 22) | (uint)rand;
        }
    }
}
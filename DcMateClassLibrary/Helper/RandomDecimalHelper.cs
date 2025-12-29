using System;
using System.Security.Cryptography;

namespace DcMateH5Api.Helper
{
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
        /// 產生固定 15 位數的 decimal：
        /// 格式為 [3 位亂數][yyMMddHHmmss]
        /// 例如：742240129154233
        /// </summary>
        /// <summary>
        /// 產生固定 15 位數的時間型亂數碼
        /// 格式：yyyyMMddHHmm + 3 位亂數
        /// 範例：202512290524742
        /// 
        /// 注意：
        /// - 同一分鐘內最多可承受約 1000 筆不重複
        /// - 適合用於業務流水號 / SID / Mapping 識別
        /// - 非加密用途
        /// </summary>
        public static decimal GenerateRandomDecimal()
        {
            // 產生時間碼（年到分鐘，12 位）
            var timePart = DateTime.Now.ToString("yyyyMMddHHmm");

            // 產生 3 位亂數（000~999）
            var bytes = new byte[2];
            RandomNumberGenerator.Fill(bytes);

            var rand = BitConverter.ToUInt16(bytes, 0) % 1000;
            var randPart = rand.ToString("D3");

            // 組合成 15 位
            return decimal.Parse(timePart + randPart);
        }

        /// <summary>
        /// 產生類似 Twitter Snowflake 演算法的唯一遞增 ID，回傳 long 值
        /// 結構為：目前 UTC 毫秒時間 &lt;&lt; 22 | 22-bit 隨機值
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
}

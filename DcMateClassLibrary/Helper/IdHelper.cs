using System;

namespace DcMateClassLibrary.Helper // 確保命名空間與同事的 Helper 資料夾一致
{
    public static class IdHelper
    {
        private static readonly Random _random = new Random();

        /// <summary>
        /// 產生 18 位數的 Decimal ID (yyyyMMddHHmmss + 4位隨機數)
        /// </summary>
        public static decimal GenerateNumericId()
        {
            // 取得時間字串 (14位)
            string timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");

            // 加上隨機數 (4位)
            string randomNumber = _random.Next(1000, 9999).ToString();

            // 轉換為 decimal (轉型失敗則回傳 0)
            return decimal.TryParse(timestamp + randomNumber, out decimal result) ? result : 0;
        }
    }
}
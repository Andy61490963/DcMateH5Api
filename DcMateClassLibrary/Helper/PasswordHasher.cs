using System.Security.Cryptography;
using DcMateH5Api.Areas.Security.Interfaces;

namespace DcMateH5Api.Helper
{
    /// <summary>
    /// 密碼雜湊工具：
    /// 使用 PBKDF2（Rfc2898DeriveBytes）搭配 SHA256 進行密碼雜湊與驗證。
    /// 每個密碼都必須搭配獨立 Salt
    /// 使用大量 Iteration 防止暴力破解與彩虹表攻擊
    /// 使用 FixedTimeEquals 防止 Timing Attack
    /// </summary>
    public class PasswordHasher : IPasswordHasher
    {
        /// <summary>
        /// PBKDF2 迭代次數。
        /// Iterations 越高，單次 Hash 計算越慢
        /// - 攻擊者暴力破解成本
        /// - 合法使用者登入延遲
        /// </summary>
        private const int Iterations = 100000;

        /// <summary>
        /// 雜湊輸出長度（單位：byte）
        /// 32 bytes = 256 bits
        /// 與 SHA256 長度對齊，安全性足夠且效能穩定
        /// </summary>
        private const int HashSize = 32;

        /// <summary>
        /// 產生隨機 Salt。
        /// 
        /// Salt 的目的：
        /// - 即使兩個使用者使用相同密碼，雜湊結果也會不同
        /// - 防止彩虹表（Rainbow Table）攻擊
        /// 
        /// 使用 RandomNumberGenerator：
        /// - 系統層級 CSPRNG
        /// - 非 pseudo-random，適合密碼學用途
        /// </summary>
        /// <param name="size">
        /// Salt 長度（byte），預設 16 bytes（128 bits）
        /// </param>
        /// <returns>
        /// Base64 編碼後的 Salt 字串（方便存 DB）
        /// </returns>
        public string GenerateSalt(int size = 16)
        {
            var saltBytes = RandomNumberGenerator.GetBytes(size);
            return Convert.ToBase64String(saltBytes);
        }

        /// <summary>
        /// 使用 PBKDF2 對密碼進行雜湊。
        /// 
        /// 注意：
        /// - 這裡「不是加密」，是單向雜湊
        /// - 無法從 Hash 還原原始密碼
        /// 
        /// 流程：
        /// 1. 將 Base64 Salt 轉回 byte[]
        /// 2. 使用 PBKDF2 + SHA256 + Iterations
        /// 3. 產生固定長度 Hash
        /// 4. 回傳 Base64 字串以便儲存
        /// </summary>
        /// <param name="password">使用者輸入的明文密碼</param>
        /// <param name="salt">Base64 編碼後的 Salt</param>
        /// <returns>Base64 編碼後的密碼雜湊</returns>
        public string HashPassword(string password, string salt)
        {
            var saltBytes = Convert.FromBase64String(salt);

            using var pbkdf2 = new Rfc2898DeriveBytes(
                password,              // 明文密碼
                saltBytes,              // Salt
                Iterations,             // 迭代次數
                HashAlgorithmName.SHA256 // 使用 SHA256
            );

            var hash = pbkdf2.GetBytes(HashSize);
            return Convert.ToBase64String(hash);
        }

        /// <summary>
        /// 驗證密碼是否正確。
        /// 
        /// 驗證方式：
        /// - 使用「相同 Salt」重新計算輸入密碼的 Hash
        /// - 與資料庫中儲存的 Hash 做比較
        /// 
        /// 使用 FixedTimeEquals：
        /// - 避免根據比對時間長短洩漏資訊
        /// - 防止 Timing Attack
        /// </summary>
        /// <param name="password">使用者輸入的明文密碼</param>
        /// <param name="storedHash">資料庫中儲存的 Hash（Base64）</param>
        /// <param name="storedSalt">資料庫中儲存的 Salt（Base64）</param>
        /// <returns>是否驗證成功</returns>
        public bool VerifyPassword(string password, string storedHash, string storedSalt)
        {
            // 使用同一個 Salt 重新計算 Hash
            var hashToCompare = HashPassword(password, storedSalt);

            var storedHashBytes = Convert.FromBase64String(storedHash);
            var hashBytes = Convert.FromBase64String(hashToCompare);

            // 使用固定時間比較，避免 timing attack
            return CryptographicOperations.FixedTimeEquals(storedHashBytes, hashBytes);
        }
    }
}

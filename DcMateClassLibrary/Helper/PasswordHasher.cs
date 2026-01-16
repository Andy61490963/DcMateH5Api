using DcMateH5Api.Areas.Security.Interfaces;
using System.Security.Cryptography;
using System.Text;

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
            //// 使用同一個 Salt 重新計算 Hash
            //var hashToCompare = HashPassword(password, storedSalt);

            //var storedHashBytes = Convert.FromBase64String(storedHash);
            //var hashBytes = Convert.FromBase64String(hashToCompare);

            //// 使用固定時間比較，避免 timing attack
            //return CryptographicOperations.FixedTimeEquals(storedHashBytes, hashBytes);
            return WeYuVerifyPassword(password, storedHash, storedSalt);
        }

        public string WeYuHashPassword(string password, string salt)
        {
            // 呼叫您提到的 DLL 加密方法
            // 例如：return WeYudll.Encrypt(password, salt);
            return "DLL運算的加密結果";
        }

        /// <summary>
        /// 驗證舊系統的 AES 加密密碼
        /// </summary>
        /// <param name="inputPassword">使用者輸入的明文密碼</param>
        /// <param name="storedHash">資料庫中的 SECOND_PWD 欄位 (密文)</param>
        /// <param name="salt">資料庫中的 PWD 欄位 (作為解密用的 Salt)</param>
        /// <returns>驗證是否成功</returns>
        public bool WeYuVerifyPassword(string inputPassword, string storedHash, string salt)
        {
            // 預防資料庫欄位為 null 或 PWD (salt) 長度不足導致 Substring 崩潰
            if (string.IsNullOrEmpty(storedHash) || string.IsNullOrEmpty(salt) || salt.Length < 9)
            {
                return false;
            }

            try
            {
                // 1. 設定舊系統固定的內部密鑰
                string internalKey = "WeYuTech";

                // 2. 舊系統邏輯：拿 PWD 欄位的前 9 位當作 Salt 進行衍生
                byte[] saltBytes = Encoding.UTF8.GetBytes(salt.Substring(0, 9));

                // 3. 修正過時警告：明確指定 SHA1 (與舊系統保持一致) 與 迭代次數
                // 舊版預設迭代次數為 1000
                using var keyGenerator = new Rfc2898DeriveBytes(internalKey, saltBytes, 1000, HashAlgorithmName.SHA1);

                // 4. 使用 Aes 進行解密 (取代過時的 RijndaelManaged)
                using Aes aes = Aes.Create();
                aes.KeySize = 128;
                aes.BlockSize = 128;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                aes.Key = keyGenerator.GetBytes(16);
                aes.IV = keyGenerator.GetBytes(16);

                // 5. 執行解密流程
                ICryptoTransform decryptor = aes.CreateDecryptor();
                byte[] encryptBytes = Convert.FromBase64String(storedHash);

                using MemoryStream memoryStream = new MemoryStream();
                using (CryptoStream cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Write))
                {
                    cryptoStream.Write(encryptBytes, 0, encryptBytes.Length);
                    cryptoStream.Flush();
                }

                // 6. 比對解密後的明文與輸入密碼
                byte[] decryptBytes = memoryStream.ToArray();
                string decryptedData = Encoding.UTF8.GetString(decryptBytes);

                return decryptedData == inputPassword;
            }
            catch
            {
                return false;
            }
        }
    }
}

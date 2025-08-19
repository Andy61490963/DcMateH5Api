using System;

namespace DynamicForm.Areas.Security.Interfaces
{
    /// <summary>
    /// 提供密碼雜湊與驗證功能的介面。
    /// </summary>
    public interface IPasswordHasher
    {
        /// <summary>
        /// 產生隨機鹽值（Base64 編碼）。
        /// </summary>
        /// <returns>Base64 編碼的鹽值字串。</returns>
        string GenerateSalt(int size = 16);

        /// <summary>
        /// 使用 PBKDF2 演算法產生密碼雜湊。
        /// </summary>
        /// <param name="password">原始密碼。</param>
        /// <param name="salt">Base64 編碼的鹽值。</param>
        /// <returns>Base64 編碼的雜湊字串。</returns>
        string HashPassword(string password, string salt);

        /// <summary>
        /// 驗證密碼是否與雜湊值相符。
        /// </summary>
        /// <param name="password">原始密碼。</param>
        /// <param name="storedHash">儲存的雜湊值。</param>
        /// <param name="storedSalt">儲存的 Base64 鹽值。</param>
        /// <returns>驗證是否通過。</returns>
        bool VerifyPassword(string password, string storedHash, string storedSalt);
    }
}

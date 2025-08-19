using System;
using System.Security.Cryptography;
using DynamicForm.Areas.Security.Interfaces;

namespace DynamicForm.Helper
{
    /// <summary>
    /// 使用 PBKDF2 產生與驗證密碼雜湊。
    /// </summary>
    public class PasswordHasher : IPasswordHasher
    {
        private const int Iterations = 100000;
        private const int HashSize = 32; // 256 bit

        /// <inheritdoc />
        public string GenerateSalt(int size = 16)
        {
            var saltBytes = RandomNumberGenerator.GetBytes(size);
            return Convert.ToBase64String(saltBytes);
        }

        /// <inheritdoc />
        public string HashPassword(string password, string salt)
        {
            var saltBytes = Convert.FromBase64String(salt);
            using var pbkdf2 = new Rfc2898DeriveBytes(password, saltBytes, Iterations, HashAlgorithmName.SHA256);
            var hash = pbkdf2.GetBytes(HashSize);
            return Convert.ToBase64String(hash);
        }

        /// <inheritdoc />
        public bool VerifyPassword(string password, string storedHash, string storedSalt)
        {
            var hashToCompare = HashPassword(password, storedSalt);
            var storedHashBytes = Convert.FromBase64String(storedHash);
            var hashBytes = Convert.FromBase64String(hashToCompare);
            return CryptographicOperations.FixedTimeEquals(storedHashBytes, hashBytes);
        }
    }
}

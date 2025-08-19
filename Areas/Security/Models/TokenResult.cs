using System;

namespace DynamicForm.Areas.Security.Models
{
    /// <summary>
    /// JWT 相關結果資訊。
    /// </summary>
    public class TokenResult
    {
        /// <summary>
        /// JWT Token 字串。
        /// </summary>
        public string Token { get; set; } = string.Empty;

        /// <summary>
        /// Token 到期時間。
        /// </summary>
        public DateTime Expiration { get; set; }

        /// <summary>
        /// Refresh Token 字串（若有）。
        /// </summary>
        public string? RefreshToken { get; set; }

        /// <summary>
        /// Refresh Token 到期時間（若有）。
        /// </summary>
        public DateTime? RefreshTokenExpiration { get; set; }
    }
}

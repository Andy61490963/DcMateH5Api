using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using DcMateH5Api.Areas.Security.Models;
using DcMateH5Api.Areas.Security.Interfaces;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace DcMateH5Api.Helper
{
    /// <summary>
    /// JWT Token 產生器。
    /// 
    /// 職責：
    /// - 根據使用者資訊產生 JWT
    /// - 將身分資訊包裝成 Claims
    /// - 使用對稱式金鑰（HMAC-SHA256）簽章
    /// 
    /// 設計重點：
    /// - Token 只負責「身分識別」，不放敏感資料
    /// - 有明確過期時間，避免長期有效 Token
    /// - 設定值全部來自設定檔
    /// </summary>
    public class JwtTokenGenerator : ITokenGenerator
    {
        /// <summary>
        /// JWT 設定（Issuer / Audience / SecretKey / ExpiresMinutes）。
        /// 
        /// 透過 IOptions 注入：
        /// - 符合 DI 與設定管理最佳實務
        /// - 可依環境（Dev / Staging / Prod）切換
        /// </summary>
        private readonly JwtSettings _jwtSettings;

        /// <summary>
        /// 初始化 JWT Token 產生器。
        /// </summary>
        /// <param name="jwtOptions">
        /// 來自 appsettings.json 的 JWT 設定物件
        /// </param>
        public JwtTokenGenerator(IOptions<JwtSettings> jwtOptions)
        {
            _jwtSettings = jwtOptions.Value;
        }

        /// <summary>
        /// 依據使用者帳號資訊產生 JWT Token。
        /// 
        /// Claim 設計原則：
        /// - Sub：使用者唯一識別（UserId）
        /// - UniqueName：登入帳號（Account）
        /// - 不放密碼、不放敏感資訊
        /// 
        /// Token 特性：
        /// - 使用 HMAC-SHA256 簽章
        /// - 有明確 Expiration
        /// - 適合搭配 ASP.NET Core JwtBearer 驗證
        /// </summary>
        /// <param name="user">已通過驗證的使用者帳號</param>
        /// <returns>
        /// TokenResult：
        /// - Token：JWT 字串
        /// - Expiration：到期時間（UTC）
        /// </returns>
        public TokenResult GenerateToken(UserAccount user)
        {
            // 建立 JWT Claims（Payload）
            var claims = new[]
            {
                // Subject：使用者唯一識別碼（通常放 UserId）
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),

                // UniqueName：登入帳號或顯示名稱
                new Claim(JwtRegisteredClaimNames.UniqueName, user.Account),

                // 角色或權限
                // new Claim(ClaimTypes.Role, user.Role)
            };

            // 使用設定檔中的 SecretKey 建立對稱式金鑰
            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(_jwtSettings.SecretKey)
            );

            // 使用 HMAC-SHA256 進行簽章
            var creds = new SigningCredentials(
                key,
                SecurityAlgorithms.HmacSha256
            );

            // 設定 Token 到期時間（使用 UTC，避免時區問題）
            var expiration = DateTime.UtcNow
                .AddMinutes(_jwtSettings.ExpiresMinutes);

            // 建立 JWT Token
            var token = new JwtSecurityToken(
                issuer: _jwtSettings.Issuer,     // 簽發者
                audience: _jwtSettings.Audience, // 接收者
                claims: claims,                  // Claims
                expires: expiration,             // 到期時間
                signingCredentials: creds        // 簽章資訊
            );

            // 將 JwtSecurityToken 轉成字串
            var tokenString = new JwtSecurityTokenHandler()
                .WriteToken(token);

            // 回傳 Token 與過期時間
            return new TokenResult
            {
                Token = tokenString,
                Expiration = expiration
            };
        }
    }
}
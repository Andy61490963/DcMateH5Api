using System;
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
    /// 使用設定檔產生 JWT Token。
    /// </summary>
    public class JwtTokenGenerator : ITokenGenerator
    {
        private readonly JwtSettings _jwtSettings;

        /// <summary>
        /// 初始化 JWT 產生器。
        /// </summary>
        /// <param name="jwtOptions">JWT 設定。</param>
        public JwtTokenGenerator(IOptions<JwtSettings> jwtOptions)
        {
            _jwtSettings = jwtOptions.Value;
        }

        /// <inheritdoc />
        public TokenResult GenerateToken(UserAccount user)
        {
            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.UniqueName, user.Account),
                // new Claim(ClaimTypes.Role, user.Role)
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.SecretKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var expiration = DateTime.UtcNow.AddMinutes(_jwtSettings.ExpiresMinutes);

            var token = new JwtSecurityToken(
                issuer: _jwtSettings.Issuer,
                audience: _jwtSettings.Audience,
                claims: claims,
                expires: expiration,
                signingCredentials: creds
            );

            var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
            return new TokenResult
            {
                Token = tokenString,
                Expiration = expiration
            };
        }
    }
}

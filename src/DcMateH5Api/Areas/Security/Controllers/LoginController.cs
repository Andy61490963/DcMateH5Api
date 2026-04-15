using ClassLibrary;
using DcMateClassLibrary.Helper;
using DcMateH5.Abstractions.Token;
using DcMateH5Api.Areas.Security.ViewModels;
using DcMateH5Api.Areas.Security.ViewModels.Login;
using DcMateH5Api.Controllers;
using DcMateH5Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DcMateH5Api.Areas.Security.Controllers
{
    /// <summary>
    /// 處理登入
    /// </summary>
    [Area("Security")]
    [ApiController]
    [ApiExplorerSettings(GroupName = SwaggerGroups.Security)]
    [Route("[area]/[controller]")]
    public class LoginController : BaseController
    {
        private static class HeaderNames
        {
            public const string Authorization = "Authorization";
            public const string BearerPrefix = "Bearer ";
            public const string TokenExpire = "X-Token-Expire";
        }

        // 使用完整命名空間來避開衝突
        private readonly Interfaces.IAuthenticationService _authService;
        private readonly ITokenService _tokenService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IConfiguration _config;
        
        /// <summary>
        /// 建構函式注入驗證服務。
        /// </summary>
        /// <param name="authService">驗證服務。</param>
        public LoginController(Interfaces.IAuthenticationService authService, IHttpContextAccessor httpContextAccessor,
        IConfiguration config,
        ITokenService tokenService)
        {
            _authService = authService;
            _tokenService = tokenService;
            _httpContextAccessor = httpContextAccessor;
            _config = config;
        }

        /// <summary>
        /// Renew current token.
        /// </summary>
        /// <returns>New token and expiration time.</returns>
        [Authorize]
        [HttpPost("renew-token")]
        [ProducesResponseType(typeof(Result<TokenInfo>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(Result<TokenInfo>), StatusCodes.Status401Unauthorized)]
        public IActionResult RenewToken()
        {
            string currentToken = ExtractBearerToken(Request);
            var renewResult = _tokenService.RenewToken(currentToken);

            if (!renewResult.IsSuccess || renewResult.ExpirationText == null)
            {
                var failedResult = Result<TokenInfo>.Fail(
                    AuthenticationErrorCode.Unauthorized,
                    string.IsNullOrWhiteSpace(renewResult.Message)
                        ? "Token renew failed."
                        : renewResult.Message);

                return Unauthorized(failedResult);
            }

            Response.Headers[HeaderNames.Authorization] =
                $"{HeaderNames.BearerPrefix}{renewResult.TokenKey}";
            Response.Headers[HeaderNames.TokenExpire] =
                renewResult.ExpirationText.Value.ToString("o");

            var response = new TokenInfo
            {
                TOKEN_STATUS = "true",
                ACCOUNT_NO = renewResult.AccountNo,
                TOKEN_KEY = renewResult.TokenKey,
                TOKEN_EXPIRY = renewResult.ExpirationText,
                TOKEN_SEQ = renewResult.TokenSeq
            };

            return Ok(Result<TokenInfo>.Ok(response));
        }

        /// <summary>
        /// 登入
        /// </summary>
        /// <param name="request">帳號與密碼。</param>
        /// <returns>Token。</returns>
        [HttpPost("login")]
        [ProducesResponseType(typeof(Result<LoginResponseViewModel>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(Result<LoginResponseViewModel>), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Login([FromBody] LoginRequestViewModel request, CancellationToken ct)
        {
            var result = await _authService.AuthenticateAsync(request.Account, request.Password, ct);
            
            if (result.IsSuccess)
            {
                return Ok(result);
            }
            
            return Unauthorized(result);
        }

        /// <summary>
        /// 登出
        /// </summary>
        /// <returns>結果。</returns>
        [Authorize]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            await _authService.LogoutAsync();
            return Ok();
        }

        private static string ExtractBearerToken(HttpRequest request)
        {
            string authorizationHeader = request.Headers[HeaderNames.Authorization].FirstOrDefault() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(authorizationHeader))
            {
                return string.Empty;
            }

            if (!authorizationHeader.StartsWith(HeaderNames.BearerPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            return authorizationHeader[HeaderNames.BearerPrefix.Length..].Trim();
        }

    }
}

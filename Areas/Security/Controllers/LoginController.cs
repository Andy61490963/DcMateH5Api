using ClassLibrary;
using DcMateH5Api.Areas.Security.Interfaces;
using DcMateH5Api.Areas.Security.ViewModels;
using DcMateH5Api.Helper;
using DcMateH5Api.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DcMateH5Api.Areas.Security.Controllers
{
    /// <summary>
    /// 處理登入與取得 JWT 的 API。
    /// </summary>
    [Area("Security")]
    [ApiController]
    [ApiExplorerSettings(GroupName = SwaggerGroups.Security)]
    [Route("[area]/[controller]")]
    public class LoginController : ControllerBase
    {
        // 使用完整命名空間來避開衝突
        private readonly DcMateH5Api.Areas.Security.Interfaces.IAuthenticationService _authService;
        private readonly IHttpContextAccessor _httpContextAccessor; // 新增注入
        private readonly IConfiguration _config; // 新增注入
        /// <summary>
        /// 建構函式注入驗證服務。
        /// </summary>
        /// <param name="authService">驗證服務。</param>
        public LoginController(DcMateH5Api.Areas.Security.Interfaces.IAuthenticationService authService, IHttpContextAccessor httpContextAccessor,
        IConfiguration config)
        {
            _authService = authService;
            _httpContextAccessor = httpContextAccessor;
            _config = config;
        }

        /// <summary>
        /// 登入並取得 JWT Token。
        /// </summary>
        /// <param name="request">帳號與密碼。</param>
        /// <returns>JWT Token。</returns>
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

        [Authorize]
        [HttpPost("extend-session")]
        public async Task<IActionResult> ExtendSession()
        {
            var result = await _authService.ExtendSessionAsync(); // 只要呼叫這行

            if (result.IsSuccess) return Ok(result.Data);

            return Unauthorized(result.Message);
        }

        /// <summary>
        /// 註冊新帳號。
        /// </summary>
        /// <param name="request">帳號、密碼、角色。</param>
        [HttpPost("register")]
        [ProducesResponseType(typeof(Result<int>), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(Result<int>), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(Result<int>), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Register([FromBody] RegisterRequestViewModel request, CancellationToken ct)
        {
            var result  = await _authService.RegisterAsync(request, ct);

            if (result.IsSuccess)
            {
                return StatusCode(StatusCodes.Status201Created, result);
            }
            
            if (result.Code == nameof(AuthenticationErrorCode.AccountAlreadyExists))
            {
                return Conflict(result);
            }

            return StatusCode(StatusCodes.Status500InternalServerError, result);
        }

        

    }
}

using DcMateH5Api.Areas.Security.Interfaces;
using DcMateH5Api.Areas.Security.ViewModels;
using Microsoft.AspNetCore.Mvc;
using DcMateH5Api.Helper;

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
        private readonly IAuthenticationService _authService;
        
        /// <summary>
        /// 建構函式注入驗證服務。
        /// </summary>
        /// <param name="authService">驗證服務。</param>
        public LoginController(IAuthenticationService authService)
        {
            _authService = authService;
        }

        /// <summary>
        /// 登入並取得 JWT Token。
        /// </summary>
        /// <param name="request">帳號與密碼。</param>
        /// <returns>JWT Token。</returns>
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequestViewModel request, CancellationToken ct)
        {
            var result = await _authService.AuthenticateAsync(request.Account, request.Password, ct);
            if (result == null)
            {
                return StatusCode(StatusCodes.Status401Unauthorized, "查無帳號"); 
            }

            return StatusCode(StatusCodes.Status200OK, result); 
        }
        
        /// <summary>
        /// 註冊新帳號。
        /// </summary>
        /// <param name="request">帳號、密碼、角色。</param>
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequestViewModel request, CancellationToken ct)
        {
            var rows = await _authService.RegisterAsync(request, ct);

            if (rows == -1)
            {
                return Conflict("帳號已存在"); 
            }

            if (rows > 0)
            {
                return StatusCode(StatusCodes.Status201Created, "註冊成功"); 
            }

            return StatusCode(StatusCodes.Status500InternalServerError, "註冊失敗，請稍後再試"); 
        }

    }
}

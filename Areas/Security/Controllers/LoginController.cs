using DynamicForm.Areas.Security.Interfaces;
using DynamicForm.Areas.Security.ViewModels;
using Microsoft.AspNetCore.Mvc;
using DynamicForm.Helper;

namespace DynamicForm.Areas.Security.Controllers
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
        private const string AccountExistsMessage = "Account already exists.";
        
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
                return Unauthorized();
            }

            return Ok(result);
        }
        
        /// <summary>
        /// 註冊新帳號。
        /// </summary>
        /// <param name="request">帳號、密碼、角色。</param>
        /// <returns>註冊成功的帳號資訊。</returns>
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequestViewModel request, CancellationToken ct)
        {
            var result = await _authService.RegisterAsync(request.Account, request.Password, ct);
            if (result == null)
            {
                return BadRequest(AccountExistsMessage);
            }
            return Ok(result);
        }

    }
}

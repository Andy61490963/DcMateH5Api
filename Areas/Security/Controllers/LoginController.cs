using DcMateH5Api.Areas.Security.ViewModels;
using DcMateH5Api.Controllers;
using DcMateH5Api.Helper;
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
        // 使用完整命名空間來避開衝突
        private readonly Interfaces.IAuthenticationService _authService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IConfiguration _config;
        
        /// <summary>
        /// 建構函式注入驗證服務。
        /// </summary>
        /// <param name="authService">驗證服務。</param>
        public LoginController(Interfaces.IAuthenticationService authService, IHttpContextAccessor httpContextAccessor,
        IConfiguration config)
        {
            _authService = authService;
            _httpContextAccessor = httpContextAccessor;
            _config = config;
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

        [Authorize]
        [HttpPost("extend-session")]
        public async Task<IActionResult> ExtendSession()
        {
            var result = await _authService.ExtendSessionAsync(); // 只要呼叫這行

            if (result.IsSuccess) return Ok(result.Data);

            return Unauthorized(result.Message);
        }

    }
}

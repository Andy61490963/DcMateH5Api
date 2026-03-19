using DcMateClassLibrary.Helper;
using DcMateH5Api.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DcMateH5Api.Areas.Security.Controllers
{
    /// <summary>
    /// 需要 JWT 授權才能存取的範例端點。
    /// </summary>
    [Area("Security")]
    [Authorize]
    [ApiController]
    [ApiExplorerSettings(GroupName = SwaggerGroups.Security)]
    [Route("[area]/[controller]")]
    public class SecureDataController : BaseController
    {
        /// <summary>
        /// 取得受保護的資料。
        /// </summary>
        /// <returns>簡單的訊息。</returns>
        [HttpGet("data")]
        public IActionResult GetSecureData()
        {
            var userinfo = CurrentUser.Account;
            return Ok(new { Message = $"This is protected data. You are {userinfo}" });
        }
        
        /// <summary>
        /// 取得受保護的資料。
        /// </summary>
        /// <returns>簡單的訊息。</returns>
        [HttpGet("GenerateRandomDecimal")]
        public IActionResult GenerateRandomDecimal()
        {
            var x = RandomHelper.GenerateRandomDecimal(CurrentUser.TokenSeq);
            return Ok(new { GenerateRandomDecimal = $"{x}" });
        }
        
    }
}

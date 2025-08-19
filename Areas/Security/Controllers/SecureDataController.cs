using ClassLibrary;
using DynamicForm.Authorization;
using DynamicForm.Helper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DynamicForm.Areas.Security.Controllers
{
    /// <summary>
    /// 需要 JWT 授權才能存取的範例端點。
    /// </summary>
[Area("Security")]
[Authorize]
[ApiController]
[ApiExplorerSettings(GroupName = SwaggerGroups.Security)]
[Route("[area]/[controller]")]
public class SecureDataController : ControllerBase
    {
        /// <summary>
        /// 取得受保護的資料。
        /// </summary>
        /// <returns>簡單的訊息。</returns>
        [RequireControllerPermission(ActionType.View)]
        [HttpGet("data")]
        public IActionResult GetSecureData()
        {
            return Ok(new { Message = "This is protected data." });
        }
    }
}

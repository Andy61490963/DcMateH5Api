using System.IdentityModel.Tokens.Jwt;
using DcMateH5Api.Areas.Permission.Interfaces;
using DcMateH5Api.Areas.Permission.ViewModels.Menu;
using DcMateH5Api.Helper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DcMateH5Api.Areas.Security.Controllers
{
    /// <summary>
    /// 取得使用者可見的側邊選單。
    /// </summary>
    [Area("Security")]
    [ApiController]
    [ApiExplorerSettings(GroupName = SwaggerGroups.Security)]
    [Route("[area]/[controller]")]
    [Authorize]
    public class MenuController : ControllerBase
    {
        private readonly IPermissionService _permissionService;

        public MenuController(IPermissionService permissionService)
        {
            _permissionService = permissionService;
        }

        /// <summary>
        /// 取得目前登入使用者可用的選單樹。
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<MenuTreeItem>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<IEnumerable<MenuTreeItem>>> Get(CancellationToken ct)
        {
            var userIdClaim = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            if (userIdClaim is null || !Guid.TryParse(userIdClaim, out var userId))
                return Unauthorized();

            var menus = await _permissionService.GetUserMenuTreeAsync(userId, ct);
            return Ok(menus);
        }
    }
}

using DcMateH5Api.Areas.Wip.Interfaces;
using DcMateH5Api.Helper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using DcMateH5Api.Areas.Wip.Model;

namespace DcMateH5Api.Areas.Wip.Controllers
{
    [Area("Wip")]
    [Route("api/[area]/[controller]")]
    [ApiExplorerSettings(GroupName = SwaggerGroups.Wip)]
    [ApiController]
    public class WipBaseSettingController : ControllerBase
    {
        private readonly IWipBaseSettingService _wipBaseSettingService;

        public WipBaseSettingController(
            IWipBaseSettingService wipBaseSettingService)
        {
            _wipBaseSettingService = wipBaseSettingService;
        }

        [HttpPost("CheckIn")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CheckIn([FromBody] WipCheckInInputDto input, CancellationToken ct)
        {
            try
            {
                await _wipBaseSettingService.CheckInAsync(input, ct);
                return Ok();
            }
            catch (HttpStatusCodeException ex)
            {
                return StatusCode((int)ex.StatusCode, ex.Message);
            }
        }
    }
}
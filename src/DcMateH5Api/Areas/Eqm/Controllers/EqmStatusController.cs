using DcMateClassLibrary.Helper;
using DcMateClassLibrary.Helper.HttpHelper;
using DcMateH5.Abstractions.Eqm;
using DcMateH5.Abstractions.Eqm.Models;
using Microsoft.AspNetCore.Mvc;

namespace DcMateH5Api.Areas.Eqm.Controllers
{
    [Area("Eqm")]
    [Route("api/[area]/[controller]")]
    [ApiExplorerSettings(GroupName = "Eqm")]
    [ApiController]
    public class EqmStatusController : ControllerBase
    {
        private static class Routes
        {
            public const string StatusChange = "StatusChange";
        }

        private readonly IEqmStatusService _eqmStatusService;

        public EqmStatusController(IEqmStatusService eqmStatusService)
        {
            _eqmStatusService = eqmStatusService;
        }

        /// <summary>
        /// 變更設備狀態，並依需求同步更新設備主檔。
        /// </summary>
        [HttpPost(Routes.StatusChange)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> StatusChange([FromBody] EqmStatusChangeInputDto input, CancellationToken ct)
        {
            try
            {
                await _eqmStatusService.StatusChangeAsync(input, ct);
                return Ok();
            }
            catch (HttpStatusCodeException ex)
            {
                return StatusCode((int)ex.StatusCode, ex.Message);
            }
        }
    }
}

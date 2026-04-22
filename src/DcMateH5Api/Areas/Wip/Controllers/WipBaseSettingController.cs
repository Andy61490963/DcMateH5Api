using DcMateClassLibrary.Helper;
using DcMateClassLibrary.Helper.HttpHelper;
using DcMateH5.Abstractions.Wip;
using DcMateH5Api.Areas.Wip.Model;
using Microsoft.AspNetCore.Mvc;

namespace DcMateH5Api.Areas.Wip.Controllers
{
    [Area("Wip")]
    [Route("api/[area]/[controller]")]
    [ApiExplorerSettings(GroupName = SwaggerGroups.Wip)]
    [ApiController]
    public class WipWoSettingController : ControllerBase
    {
        private static class Routes
        {
            public const string CheckInWip = "CheckInWip";
            public const string CheckInCancel = "CheckInCancel";
            public const string AddWipDetails = "AddWipDetails";
            public const string EditWipDetails = "EditWipDetails";
            public const string CheckOut = "CheckOut";

            public const string CheckInAddDetailsCheckOut = "CheckInAddDetailsCheckOut";
        }

        private readonly IWipBaseSettingService _wipBaseSettingService;

        public WipWoSettingController(IWipBaseSettingService wipBaseSettingService)
        {
            _wipBaseSettingService = wipBaseSettingService;
        }

        [HttpPost(Routes.CheckInWip)]
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

        [HttpPost(Routes.CheckInCancel)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CheckInCancel([FromBody] WipCheckInCancelInputDto input, CancellationToken ct)
        {
            try
            {
                await _wipBaseSettingService.CheckInCancelAsync(input, ct);
                return Ok();
            }
            catch (HttpStatusCodeException ex)
            {
                return StatusCode((int)ex.StatusCode, ex.Message);
            }
        }
        
        [HttpPost(Routes.AddWipDetails)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> AddDetails([FromBody] WipAddDetailInputDto input, CancellationToken ct)
        {
            try
            {
                await _wipBaseSettingService.AddDetailsAsync(input, ct);
                return Ok();
            }
            catch (HttpStatusCodeException ex)
            {
                return StatusCode((int)ex.StatusCode, ex.Message);
            }
        }

        [HttpPost(Routes.EditWipDetails)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> EditDetails([FromBody] WipEditDetailInputDto input, CancellationToken ct)
        {
            try
            {
                await _wipBaseSettingService.EditDetailsAsync(input, ct);
                return Ok();
            }
            catch (HttpStatusCodeException ex)
            {
                return StatusCode((int)ex.StatusCode, ex.Message);
            }
        }

        [HttpPost(Routes.CheckOut)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CheckOut([FromBody] WipCheckOutInputDto input, CancellationToken ct)
        {
            try
            {
                await _wipBaseSettingService.CheckOutAsync(input, ct);
                return Ok();
            }
            catch (HttpStatusCodeException ex)
            {
                return StatusCode((int)ex.StatusCode, ex.Message);
            }
        }

        /// <summary>
        /// 一次完成：CheckIn → AddDetails → CheckOut（同一個 Request 內完成）
        /// </summary>
        /// <remarks>
        /// - 行為與既有三支 API 完全一致，只是串成一個流程
        /// - 三個動作在同一個 DB Transaction 內：任何一步失敗都會 rollback
        /// </remarks>
        [HttpPost(Routes.CheckInAddDetailsCheckOut)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CheckInAddDetailsCheckOut([FromBody] WipCheckInAddDetailsCheckOutInputDto input, CancellationToken ct)
        {
            try
            {
                await _wipBaseSettingService.CheckInAddDetailsCheckOutAsync(input, ct);
                return Ok();
            }
            catch (HttpStatusCodeException ex)
            {
                return StatusCode((int)ex.StatusCode, ex.Message);
            }
        }
    }
}

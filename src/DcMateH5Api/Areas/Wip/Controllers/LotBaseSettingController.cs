using ClassLibrary;
using DcMateClassLibrary.Helper;
using DcMateClassLibrary.Helper.HttpHelper;
using DcMateH5.Abstractions.Wip;
using DcMateH5Api.Areas.Wip.Model;
using DcMateH5Api.Models;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace DcMateH5Api.Areas.Wip.Controllers;

[Area("Wip")]
[Route("api/[area]/[controller]")]
[ApiExplorerSettings(GroupName = SwaggerGroups.Wip)]
[ApiController]
public class WipLotSettingController : ControllerBase
{
    private static class Routes
    {
        public const string CreateLot = "CreateLot";
        public const string LotCheckIn = "LotCheckIn";
        public const string LotCheckInCancel = "LotCheckInCancel";
        public const string LotCheckOut = "LotCheckOut";
        public const string LotReassignOperation = "LotReassignOperation";
        public const string LotRecordDC = "LotRecordDC";
        public const string LotHold = "LotHold";
        public const string LotHoldRelease = "LotHoldRelease";
    }

    private readonly ILotBaseSettingService _lotBaseSettingService;

    public WipLotSettingController(ILotBaseSettingService lotBaseSettingService)
    {
        _lotBaseSettingService = lotBaseSettingService;
    }

    /// <summary>
    /// 新增 lot
    /// </summary>
    /// <param name="input"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    [HttpPost(Routes.CreateLot)]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateLot([FromBody] WipCreateLotInputDto input, CancellationToken ct)
    {
        try
        {
            return Ok(await _lotBaseSettingService.CreateLotAsync(input, ct));
        }
        catch (HttpStatusCodeException ex)
        {
            return BuildErrorResult(ex.StatusCode, ex.Message);
        }
        catch (Exception ex)
        {
            return BuildErrorResult(HttpStatusCode.InternalServerError, ex.Message);
        }
    }

    /// <summary>
    /// 進站
    /// </summary>
    /// <param name="input"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    [HttpPost(Routes.LotCheckIn)]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> LotCheckIn([FromBody] WipLotCheckInInputDto input, CancellationToken ct)
    {
        try
        {
            return Ok(await _lotBaseSettingService.LotCheckInAsync(input, ct));
        }
        catch (HttpStatusCodeException ex)
        {
            return BuildErrorResult(ex.StatusCode, ex.Message);
        }
        catch (Exception ex)
        {
            return BuildErrorResult(HttpStatusCode.InternalServerError, ex.Message);
        }
    }

    /// <summary>
    /// 取消進站
    /// </summary>
    /// <param name="input"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    [HttpPost(Routes.LotCheckInCancel)]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> LotCheckInCancel([FromBody] WipLotCheckInCancelInputDto input, CancellationToken ct)
    {
        try
        {
            return Ok(await _lotBaseSettingService.LotCheckInCancelAsync(input, ct));
        }
        catch (HttpStatusCodeException ex)
        {
            return BuildErrorResult(ex.StatusCode, ex.Message);
        }
        catch (Exception ex)
        {
            return BuildErrorResult(HttpStatusCode.InternalServerError, ex.Message);
        }
    }

    /// <summary>
    /// 出站
    /// </summary>
    /// <param name="input"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    [HttpPost(Routes.LotCheckOut)]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> LotCheckOut([FromBody] WipLotCheckOutInputDto input, CancellationToken ct)
    {
        try
        {
            return Ok(await _lotBaseSettingService.LotCheckOutAsync(input, ct));
        }
        catch (HttpStatusCodeException ex)
        {
            return BuildErrorResult(ex.StatusCode, ex.Message);
        }
        catch (Exception ex)
        {
            return BuildErrorResult(HttpStatusCode.InternalServerError, ex.Message);
        }
    }

    /// <summary>
    /// 把 lot 分派到其他站點
    /// </summary>
    /// <param name="input"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    [HttpPost(Routes.LotReassignOperation)]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> LotReassignOperation([FromBody] WipLotReassignOperationInputDto input, CancellationToken ct)
    {
        try
        {
            return Ok(await _lotBaseSettingService.LotReassignOperationAsync(input, ct));
        }
        catch (HttpStatusCodeException ex)
        {
            return BuildErrorResult(ex.StatusCode, ex.Message);
        }
        catch (Exception ex)
        {
            return BuildErrorResult(HttpStatusCode.InternalServerError, ex.Message);
        }
    }

    /// <summary>
    /// 這隻先不理
    /// </summary>
    /// <param name="input"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    [HttpPost(Routes.LotRecordDC)]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> LotRecordDC([FromBody] WipLotRecordDcInputDto input, CancellationToken ct)
    {
        try
        {
            return Ok(await _lotBaseSettingService.LotRecordDcAsync(input, ct));
        }
        catch (HttpStatusCodeException ex)
        {
            return BuildErrorResult(ex.StatusCode, ex.Message);
        }
        catch (Exception ex)
        {
            return BuildErrorResult(HttpStatusCode.InternalServerError, ex.Message);
        }
    }

    /// <summary>
    /// 鎖住 特定 lot
    /// </summary>
    /// <param name="input"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    [HttpPost(Routes.LotHold)]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> LotHold([FromBody] WipLotHoldInputDto input, CancellationToken ct)
    {
        try
        {
            return Ok(await _lotBaseSettingService.LotHoldAsync(input, ct));
        }
        catch (HttpStatusCodeException ex)
        {
            return BuildErrorResult(ex.StatusCode, ex.Message);
        }
        catch (Exception ex)
        {
            return BuildErrorResult(HttpStatusCode.InternalServerError, ex.Message);
        }
    }

    /// <summary>
    /// 解除 特定 lot
    /// </summary>
    /// <param name="input"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    [HttpPost(Routes.LotHoldRelease)]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> LotHoldRelease([FromBody] WipLotHoldReleaseInputDto input, CancellationToken ct)
    {
        try
        {
            return Ok(await _lotBaseSettingService.LotHoldReleaseAsync(input, ct));
        }
        catch (HttpStatusCodeException ex)
        {
            return BuildErrorResult(ex.StatusCode, ex.Message);
        }
        catch (Exception ex)
        {
            return BuildErrorResult(HttpStatusCode.InternalServerError, ex.Message);
        }
    }

    private IActionResult BuildErrorResult(HttpStatusCode statusCode, string message)
    {
        var code = statusCode switch
        {
            HttpStatusCode.BadRequest => WipLotErrorCode.BadRequest,
            HttpStatusCode.Conflict => WipLotErrorCode.Conflict,
            _ => WipLotErrorCode.UnhandledException
        };

        return StatusCode((int)statusCode, Result<bool>.Fail(code, message));
    }
}

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
        public const string LotBonus = "LotBonus";
        public const string LotScrap = "LotScrap";
        public const string LotStateChange = "LotStateChange";
        public const string LotTerminated = "LotTerminated";
        public const string LotUnTerminated = "LotUnTerminated";
        public const string LotFinished = "LotFinished";
        public const string LotUnFinished = "LotUnFinished";
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

    /// <summary>
    /// 追加 LOT 數量，會寫入 LOT_BONUS 交易紀錄與原因紀錄。
    /// </summary>
    /// <param name="input">追加數量與原因資料。</param>
    /// <param name="ct"></param>
    /// <returns></returns>
    [HttpPost(Routes.LotBonus)]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> LotBonus([FromBody] WipLotBonusInputDto input, CancellationToken ct)
    {
        try
        {
            return Ok(await _lotBaseSettingService.LotBonusAsync(input, ct));
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
    /// 報廢 LOT 數量，會寫入 LOT_NG 交易紀錄與原因紀錄。
    /// </summary>
    /// <param name="input">報廢數量與原因資料。</param>
    /// <param name="ct"></param>
    /// <returns></returns>
    [HttpPost(Routes.LotScrap)]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> LotScrap([FromBody] WipLotScrapInputDto input, CancellationToken ct)
    {
        try
        {
            return Ok(await _lotBaseSettingService.LotScrapAsync(input, ct));
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
    /// 變更 LOT 狀態，會寫入狀態異動履歷與原因履歷。
    /// </summary>
    /// <param name="input">LOT 狀態變更資料。</param>
    /// <param name="ct"></param>
    /// <returns></returns>
    [HttpPost(Routes.LotStateChange)]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> LotStateChange([FromBody] WipLotStateChangeInputDto input, CancellationToken ct)
    {
        try
        {
            return Ok(await _lotBaseSettingService.LotStateChangeAsync(input, ct));
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
    /// 將 Wait 狀態 LOT 結束為 Terminated。
    /// </summary>
    /// <param name="input">LOT 狀態動作資料。</param>
    /// <param name="ct"></param>
    /// <returns></returns>
    [HttpPost(Routes.LotTerminated)]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> LotTerminated([FromBody] WipLotStatusActionInputDto input, CancellationToken ct)
    {
        try
        {
            return Ok(await _lotBaseSettingService.LotTerminatedAsync(input, ct));
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
    /// 將 Terminated 狀態 LOT 還原為 Wait。
    /// </summary>
    /// <param name="input">LOT 狀態動作資料。</param>
    /// <param name="ct"></param>
    /// <returns></returns>
    [HttpPost(Routes.LotUnTerminated)]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> LotUnTerminated([FromBody] WipLotStatusActionInputDto input, CancellationToken ct)
    {
        try
        {
            return Ok(await _lotBaseSettingService.LotUnTerminatedAsync(input, ct));
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
    /// 將 Wait 狀態 LOT 完工為 Finished。
    /// </summary>
    /// <param name="input">LOT 狀態動作資料。</param>
    /// <param name="ct"></param>
    /// <returns></returns>
    [HttpPost(Routes.LotFinished)]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> LotFinished([FromBody] WipLotStatusActionInputDto input, CancellationToken ct)
    {
        try
        {
            return Ok(await _lotBaseSettingService.LotFinishedAsync(input, ct));
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
    /// 將 Finished 狀態 LOT 還原為 Wait。
    /// </summary>
    /// <param name="input">LOT 狀態動作資料。</param>
    /// <param name="ct"></param>
    /// <returns></returns>
    [HttpPost(Routes.LotUnFinished)]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> LotUnFinished([FromBody] WipLotStatusActionInputDto input, CancellationToken ct)
    {
        try
        {
            return Ok(await _lotBaseSettingService.LotUnFinishedAsync(input, ct));
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

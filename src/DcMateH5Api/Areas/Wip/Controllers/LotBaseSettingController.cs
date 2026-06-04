using ClassLibrary;
using DcMateClassLibrary.Helper;
using DcMateClassLibrary.Helper.HttpHelper;
using DcMateH5.Abstractions.Wip;
using DcMateH5Api.Areas.Wip.Model;
using DcMateH5Api.Models;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace DcMateH5Api.Areas.Wip.Controllers;

/// <summary>
/// 提供 WIP LOT 生命週期相關 API。
/// Controller 只負責路由、呼叫服務與統一錯誤回應；驗證與資料庫狀態異動由 ILotBaseSettingService 處理。
/// </summary>
[Area("Wip")]
[Route("api/[area]/[controller]")]
[ApiExplorerSettings(GroupName = SwaggerGroups.Wip)]
[ApiController]
public class WipLotSettingController : ControllerBase
{
    // 集中管理 action route 名稱，避免 attribute、測試與文件各自維護後不一致。
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
    /// 新增一筆或多筆 LOT，並建立第一站點的進站前置歷程。
    /// </summary>
    /// <param name="input">LOT 建立資料；每筆資料由 service 獨立處理。</param>
    /// <param name="ct">請求取消權杖。</param>
    [HttpPost(Routes.CreateLot)]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateLot([FromBody] IEnumerable<WipCreateLotInputDto> input, CancellationToken ct)
    {
        try
        {
            return Ok(await _lotBaseSettingService.CreateLotsAsync(input, ct));
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
    /// 將一筆或多筆 LOT 進站至目前站點，狀態由 Wait 轉為 Run。
    /// </summary>
    /// <param name="input">進站資料；批次中的單筆失敗會回傳在 service result。</param>
    /// <param name="ct">請求取消權杖。</param>
    [HttpPost(Routes.LotCheckIn)]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> LotCheckIn([FromBody] IEnumerable<WipLotCheckInInputDto> input, CancellationToken ct)
    {
        try
        {
            return Ok(await _lotBaseSettingService.LotCheckInsAsync(input, ct));
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
    /// 取消 LOT 目前有效的進站紀錄，並將狀態由 Run 還原為 Wait。
    /// </summary>
    /// <param name="input">取消進站資料。</param>
    /// <param name="ct">請求取消權杖。</param>
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
    /// 將一筆或多筆 LOT 從目前站點出站。
    /// Service 會依 route 推進到下一站，若已無下一站則將 LOT 標記為 Finished。
    /// </summary>
    /// <param name="input">出站資料；批次中的單筆失敗會回傳在 service result。</param>
    /// <param name="ct">請求取消權杖。</param>
    [HttpPost(Routes.LotCheckOut)]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> LotCheckOut([FromBody] IEnumerable<WipLotCheckOutInputDto> input, CancellationToken ct)
    {
        try
        {
            return Ok(await _lotBaseSettingService.LotCheckOutsAsync(input, ct));
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
    /// 將 LOT 重新分派到同一路線中的其他站點順序。
    /// </summary>
    /// <param name="input">目標站點順序與 LOT 相關資料。</param>
    /// <param name="ct">請求取消權杖。</param>
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
    /// 記錄 LOT 目前站點的資料收集值。
    /// </summary>
    /// <param name="input">資料收集主檔與明細值。</param>
    /// <param name="ct">請求取消權杖。</param>
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
    /// 在 LOT 目前狀態允許時將 LOT Hold。
    /// </summary>
    /// <param name="input">Hold 原因與 LOT 相關資料。</param>
    /// <param name="ct">請求取消權杖。</param>
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
    /// 解除 LOT 目前有效的 Hold 紀錄，並還原 LOT 狀態。
    /// </summary>
    /// <param name="input">解除 Hold 資料；LOT_HOLD_SID 必須指向尚未解除的 Hold 歷程。</param>
    /// <param name="ct">請求取消權杖。</param>
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
    /// 追加 LOT 數量，並寫入數量調整原因歷程。
    /// </summary>
    /// <param name="input">追加數量資料。</param>
    /// <param name="ct">請求取消權杖。</param>
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
    /// 報廢 LOT 數量，並寫入 NG 數量調整原因歷程。
    /// </summary>
    /// <param name="input">報廢數量資料。</param>
    /// <param name="ct">請求取消權杖。</param>
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
    /// 將 LOT 變更為呼叫端指定的目標狀態。
    /// </summary>
    /// <param name="input">包含 NEW_STATE_CODE 的狀態變更資料。</param>
    /// <param name="ct">請求取消權杖。</param>
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
    /// 將 LOT 由 Wait 或 Hold 轉為 Terminated。
    /// </summary>
    /// <param name="input">固定狀態動作資料。</param>
    /// <param name="ct">請求取消權杖。</param>
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
    /// 將 LOT 由 Terminated 還原為 Wait。
    /// </summary>
    /// <param name="input">固定狀態動作資料。</param>
    /// <param name="ct">請求取消權杖。</param>
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
    /// 將 LOT 由 Wait 轉為 Finished。
    /// </summary>
    /// <param name="input">固定狀態動作資料。</param>
    /// <param name="ct">請求取消權杖。</param>
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
    /// 將 LOT 由 Finished 還原為 Wait。
    /// </summary>
    /// <param name="input">固定狀態動作資料。</param>
    /// <param name="ct">請求取消權杖。</param>
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
        // Service 會用 HttpStatusCodeException 表示預期內的業務錯誤。
        // 這裡統一讓 HTTP status 與 Result<bool> 的錯誤代碼保持一致。
        var code = statusCode switch
        {
            HttpStatusCode.BadRequest => WipLotErrorCode.BadRequest,
            HttpStatusCode.Conflict => WipLotErrorCode.Conflict,
            _ => WipLotErrorCode.UnhandledException
        };

        return StatusCode((int)statusCode, Result<bool>.Fail(code, message));
    }
}

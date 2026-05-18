using DcMateClassLibrary.Helper;
using DcMateClassLibrary.Helper.HttpHelper;
using DcMateH5.Abstractions.Wip;
using DcMateH5Api.Areas.Wip.Model;
using DcMateH5Api.Models;
using Microsoft.AspNetCore.Mvc;

namespace DcMateH5Api.Areas.Wip.Controllers;

/// <summary>
/// 處理模具上模與下模相關 WIP 作業。
/// </summary>
[Area("Wip")]
[Route("api/[area]/[controller]")]
[ApiExplorerSettings(GroupName = SwaggerGroups.Wip)]
[ApiController]
public class WipModelUploadSettingController : ControllerBase
{
    private static class Routes
    {
        public const string ModelUploadCheckIn = "ModelUploadCheckIn";
        public const string ModelUploadCheckOut = "ModelUploadCheckOut";
        public const string EditModelUploadCav = "EditModelUploadCav";
        public const string EditModelUploadEnd = "EditModelUploadEnd";
        public const string EditModelRemoveStart = "EditModelRemoveStart";
    }

    private readonly IWipBaseSettingService _wipBaseSettingService;

    public WipModelUploadSettingController(IWipBaseSettingService wipBaseSettingService)
    {
        _wipBaseSettingService = wipBaseSettingService;
    }

    /// <summary>
    /// 上模開始，於同一個交易中建立 TOL、HIST 與 CAV 紀錄。
    /// </summary>
    /// <param name="input">上模開始進站資料。</param>
    /// <param name="ct">取消權杖。</param>
    /// <returns>新建立的 TOL SID 與 HIST SID 清單。</returns>
    [HttpPost(Routes.ModelUploadCheckIn)]
    [ProducesResponseType(typeof(Result<WipModelUploadCheckInResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ModelUploadCheckIn([FromBody] WipModelUploadCheckInInputDto input, CancellationToken ct)
    {
        try
        {
            var result = await _wipBaseSettingService.ModelUploadCheckInAsync(input, ct);
            return Ok(Result<WipModelUploadCheckInResponseDto>.Ok(result));
        }
        catch (HttpStatusCodeException ex)
        {
            return StatusCode((int)ex.StatusCode, ex.Message);
        }
    }

    /// <summary>
    /// 下模結束，更新下模完成時間並關閉相關 HIST 與未結束的 CAV 紀錄。
    /// </summary>
    /// <param name="input">TOL SID 與下模結束時間。</param>
    /// <param name="ct">取消權杖。</param>
    /// <returns>更新成功時回傳 HTTP 200。</returns>
    [HttpPost(Routes.ModelUploadCheckOut)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ModelUploadCheckOut([FromBody] WipModelUploadCheckOutInputDto input, CancellationToken ct)
    {
        try
        {
            await _wipBaseSettingService.ModelUploadCheckOutAsync(input, ct);
            return Ok();
        }
        catch (HttpStatusCodeException ex)
        {
            return StatusCode((int)ex.StatusCode, ex.Message);
        }
    }

    /// <summary>
    /// 更新單筆上模 CAV 數值。
    /// </summary>
    /// <param name="input">CAV SID 與新的 OPI_CAV 數值。</param>
    /// <param name="ct">取消權杖。</param>
    /// <returns>更新成功時回傳 HTTP 200。</returns>
    [HttpPost(Routes.EditModelUploadCav)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> EditModelUploadCav([FromBody] WipEditModelUploadCavInputDto input, CancellationToken ct)
    {
        try
        {
            await _wipBaseSettingService.EditModelUploadCavAsync(input, ct);
            return Ok();
        }
        catch (HttpStatusCodeException ex)
        {
            return StatusCode((int)ex.StatusCode, ex.Message);
        }
    }

    /// <summary>
    /// 更新上模結束時間。
    /// </summary>
    /// <param name="input">TOL SID 與 MODLE_UPLOAD_END 時間。</param>
    /// <param name="ct">取消權杖。</param>
    /// <returns>更新成功時回傳 HTTP 200。</returns>
    [HttpPost(Routes.EditModelUploadEnd)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> EditModelUploadEnd([FromBody] WipEditModelUploadEndInputDto input, CancellationToken ct)
    {
        try
        {
            await _wipBaseSettingService.EditModelUploadEndAsync(input, ct);
            return Ok();
        }
        catch (HttpStatusCodeException ex)
        {
            return StatusCode((int)ex.StatusCode, ex.Message);
        }
    }

    /// <summary>
    /// 更新下模開始時間。
    /// </summary>
    /// <param name="input">TOL SID 與 MODLE_REMOVE_START 時間。</param>
    /// <param name="ct">取消權杖。</param>
    /// <returns>更新成功時回傳 HTTP 200。</returns>
    [HttpPost(Routes.EditModelRemoveStart)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> EditModelRemoveStart([FromBody] WipEditModelRemoveStartInputDto input, CancellationToken ct)
    {
        try
        {
            await _wipBaseSettingService.EditModelRemoveStartAsync(input, ct);
            return Ok();
        }
        catch (HttpStatusCodeException ex)
        {
            return StatusCode((int)ex.StatusCode, ex.Message);
        }
    }
}

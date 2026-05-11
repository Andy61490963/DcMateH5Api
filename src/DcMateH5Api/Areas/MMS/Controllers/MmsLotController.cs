using ClassLibrary;
using DcMateClassLibrary.Helper;
using DcMateClassLibrary.Helper.HttpHelper;
using DcMateH5.Abstractions.Mms;
using DcMateH5.Abstractions.Mms.Models;
using DcMateH5Api.Models;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace DcMateH5Api.Areas.MMS.Controllers;

[Area("MMS")]
[Route("api/[area]/[controller]")]
[ApiExplorerSettings(GroupName = SwaggerGroups.Mms)]
[ApiController]
public class MmsLotController : ControllerBase
{
    private static class Routes
    {
        public const string CreateMLot = "CreateMLot";
        public const string MLotConsume = "MLotConsume";
        public const string MLotUNConsume = "MLotUNConsume";
        public const string MLotStateChange = "MLotStateChange";
    }

    private readonly IMmsLotService _mmsLotService;

    public MmsLotController(IMmsLotService mmsLotService)
    {
        _mmsLotService = mmsLotService;
    }

    /// <summary>
    /// 建立一筆 MLOT 庫存，沿用舊版 CreateMLot API 名稱。
    /// </summary>
    [HttpPost(Routes.CreateMLot)]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateMLot([FromBody] MmsCreateMLotInputDto input, CancellationToken ct)
    {
        try
        {
            return Ok(await _mmsLotService.CreateMLotAsync(input, ct));
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
    /// 消耗 MLOT 庫存，沿用舊版 MLotConsume API 名稱。
    /// </summary>
    [HttpPost(Routes.MLotConsume)]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> MLotConsume([FromBody] MmsMLotConsumeInputDto input, CancellationToken ct)
    {
        try
        {
            return Ok(await _mmsLotService.MLotConsumeAsync(input, ct));
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
    /// 取消消耗 MLOT 庫存，沿用舊版 MLotUNConsume API 名稱。
    /// </summary>
    [HttpPost(Routes.MLotUNConsume)]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> MLotUNConsume([FromBody] MmsMLotUNConsumeInputDto input, CancellationToken ct)
    {
        try
        {
            return Ok(await _mmsLotService.MLotUNConsumeAsync(input, ct));
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
    /// 變更 MLOT 狀態，沿用舊版 MLotStateChange API 名稱。
    /// </summary>
    [HttpPost(Routes.MLotStateChange)]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> MLotStateChange([FromBody] MmsMLotStateChangeInputDto input, CancellationToken ct)
    {
        try
        {
            return Ok(await _mmsLotService.MLotStateChangeAsync(input, ct));
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
            HttpStatusCode.BadRequest => MmsLotErrorCode.BadRequest,
            HttpStatusCode.Conflict => MmsLotErrorCode.Conflict,
            _ => MmsLotErrorCode.UnhandledException
        };

        return StatusCode((int)statusCode, Result<bool>.Fail(code, message));
    }
}

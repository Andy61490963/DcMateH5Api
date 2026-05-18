using DcMateClassLibrary.Helper;
using DcMateClassLibrary.Helper.HttpHelper;
using DcMateH5.Abstractions.EQM;
using DcMateH5.Abstractions.EQM.Models;
using Microsoft.AspNetCore.Mvc;

namespace DcMateH5Api.Areas.Eqm.Controllers;

[Area("Eqm")]
[Route("api/[area]/[controller]")]
[ApiExplorerSettings(GroupName = "Eqm")]
[ApiController]
public class EqmAutoDcController : ControllerBase
{
    private static class Routes
    {
        public const string AutoDcUpload = "AutoDcUpload";
    }

    private readonly IEqmAutoDcService _eqmAutoDcService;

    public EqmAutoDcController(IEqmAutoDcService eqmAutoDcService)
    {
        _eqmAutoDcService = eqmAutoDcService;
    }

    /// <summary>
    /// 自動資料收集上傳 (POST 標準版)
    /// </summary>
    /// <remarks>
    /// ### 使用說明
    /// GET版本範例
    /// 1. **WIP 模式 (預設，不帶 Mode 參數)**
    /// http://[Server_IP]/api/Eqm/EqmAutoDc/AutoDcUpload?EQP_NO=ME01&amp;VALUE=Temperature:25.8,Qty:1020&amp;SameChange=TRUE&amp;AutoIdle=TRUE
    /// 2. **EDC 模式 (網址最後必須強帶 &amp;Mode=EDC)**
    /// http://[Server_IP]/api/Eqm/EqmAutoDc/AutoDcUpload?EQP_NO=ME01&amp;VALUE=Pressure:6.5&amp;Mode=EDC&amp;SameChange=TRUE&amp;AutoIdle=TRUE
    /// 
    /// 1. **數據拆解規格 (`Value`)**
    ///     - 格式固定為 `項目代碼:數值`。
    ///     - 若有多個 Sensor 項目，請以 **「半形逗號 (,)」** 隔開。
    ///     - SameChange 為機況相同時是否要呼叫一次機況切換api
    ///     - AutoIdle 為 當差異值為0自動切成 Idle , 反之 切成Run
    /// 2. **計算模式 (`Mode`)**
    ///     - **初次上傳**：若該項目在系統內無任何歷史紀錄，則本次寫入歷史表的差異值一律強制歸零 (`0`)。
    /// 
    /// ### Mode 運算模式對照表
    /// <table>
    ///   <tr><th>模式 (Mode)</th><th>預設值</th><th>核心差異值 (Diff) 計算邏輯</th></tr>
    ///   <tr><td><b>WIP</b></td><td>★ 是</td><td>適合累加型產量。若 <code>這次值 &lt; 上次值</code> (現場清機或斷電歸零)，差異值無條件等於這次的值，自動作為全新增量的起跳點。</td></tr>
    ///   <tr><td><b>EDC</b></td><td>否</td><td>適合起伏型環境數據（如溫度、壓力）。不做任何智慧處理，一律強制執行 <code>這次值 - 上次值</code> 的純算術相減。</td></tr>
    /// </table>
    /// 
    /// ### POST 範例請求 (JSON 格式)
    /// ```json
    /// {
    ///   "EqmMasterNo": "ME01",
    ///   "Value": "Temperature:36.5,Power:150.5,Pressure:6.2",
    ///   "Mode": "WIP",
    ///   "AutoIdle": "FALSE",
    ///   "SameChange": "FALSE"
    /// }
    /// ```
    /// 
    /// ### 範例回應
    /// ```json
    /// {
    ///   "IsSuccess": true,
    ///   "Data": true,
    ///   "Code": "",
    ///   "Message": "",
    ///   "ErrorData": null
    /// }
    /// ```
    /// </remarks>
    [HttpPost(Routes.AutoDcUpload)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AutoDcUpload([FromBody] EqmAutoDcInputDto input, CancellationToken ct)
    {
        try
        {
            var result = await _eqmAutoDcService.ProcessAutoDcUploadAsync(input, ct);
            return Ok(result); // 完美對齊原本的 Result<bool> 回傳格式
        }
        catch (HttpStatusCodeException ex)
        {
            return StatusCode((int)ex.StatusCode, ex.Message);
        }
    }

    /// <summary>
    /// 自動資料收集上傳 (GET 舊硬體/CGI 相容版)
    /// </summary>
    [HttpGet(Routes.AutoDcUpload)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AutoDcUploadGet([FromQuery] EqmAutoDcInputDto input, CancellationToken ct)
    {
        try
        {
            var result = await _eqmAutoDcService.ProcessAutoDcUploadAsync(input, ct);
            return Ok(result);
        }
        catch (HttpStatusCodeException ex)
        {
            return StatusCode((int)ex.StatusCode, ex.Message);
        }
    }
}
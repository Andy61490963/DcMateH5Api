using System.Net;
using DcMateClassLibrary.Helper;
using DcMateClassLibrary.Helper.HttpHelper;
using DcMateH5.Abstractions.Prj;
using DcMateH5.Abstractions.Prj.Models;
using DcMateH5Api.Models;
using Microsoft.AspNetCore.Mvc;

namespace DcMateH5Api.Areas.PRJ.Controllers;

/// <summary>
/// 提供專案主檔、工作明細及前端選項資料。
/// </summary>
[Area("PRJ")]
[Route("api/PRJ")]
[ApiExplorerSettings(GroupName = SwaggerGroups.Prj)]
[ApiController]
[ProducesResponseType(typeof(Result<object>), StatusCodes.Status500InternalServerError)]
public sealed class PrjController : ControllerBase
{
    private readonly IPrjService _service;

    public PrjController(IPrjService service) => _service = service;

    /// <summary>分頁查詢專案列表。</summary>
    /// <remarks>
    /// 用於專案首頁、搜尋列與篩選面板。前端首次進入頁面及變更頁碼、關鍵字、狀態、類型、客戶、日期或排序時呼叫。
    /// Page 從 1 開始，PageSize 為 1～100；SortBy 支援 Seq、ProjectCode、ProjectName、StartTime、ExpectedTime、EditTime。
    /// 日期為 ISO 8601；資料庫中的 1900-01-01 會回傳 null。成功回傳分頁資料，輸入錯誤回傳 400，未登入／無權限回傳 401／403，非預期錯誤回傳 500。
    /// </remarks>
    [HttpGet("Projects")]
    [ProducesResponseType(typeof(Result<PagedResult<PrjProjectListItemDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Result<object>), StatusCodes.Status500InternalServerError)]
    public Task<IActionResult> GetProjects([FromQuery] PrjProjectQuery query, CancellationToken ct) =>
        ExecuteAsync(() => _service.GetProjectsAsync(query, ct));

    /// <summary>取得單一專案完整資料。</summary>
    /// <remarks>
    /// 用於專案檢視頁與編輯頁初始化。前端應將路由中的 projectCode 做 URL encode；回傳包含顯示名稱、工作總數、完成數、逾期數及 EditTime。
    /// 後續修改、停用或排序時必須帶回最新 EditTime。找不到專案回傳 404，未登入／無權限回傳 401／403。
    /// </remarks>
    /// <param name="projectCode">不可為空白的專案代碼。</param>
    [HttpGet("Projects/{projectCode}")]
    [ProducesResponseType(typeof(Result<PrjProjectDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(Result<object>), StatusCodes.Status500InternalServerError)]
    public Task<IActionResult> GetProject([FromRoute] string projectCode, CancellationToken ct) =>
        ExecuteAsync(() => _service.GetProjectAsync(projectCode, ct));

    /// <summary>建立新專案。</summary>
    /// <remarks>
    /// 用於「新增專案」表單送出。ProjectCode 建立後不可修改且不可重複；狀態、類型及客戶代碼應先由 Options API 取得。
    /// 前端不需傳稽核欄位，後端會使用登入帳號。日期使用 ISO 8601，空日期傳 null，不可傳 1900-01-01。
    /// 成功回傳 201 與建立後資料；欄位或選項錯誤回傳 400，代碼重複回傳 409。
    /// </remarks>
    [HttpPost("Projects")]
    [ProducesResponseType(typeof(Result<PrjProjectDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(Result<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Result<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(Result<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateProject([FromBody] CreatePrjProjectRequest request, CancellationToken ct)
    {
        try
        {
            var data = await _service.CreateProjectAsync(request, ct);
            return CreatedAtAction(nameof(GetProject), new { projectCode = data.ProjectCode }, Result<PrjProjectDto>.Ok(data));
        }
        catch (HttpStatusCodeException ex)
        {
            return BuildError(ex);
        }
    }

    /// <summary>修改專案基本資料。</summary>
    /// <remarks>
    /// 用於專案編輯表單儲存，不允許更換 ProjectCode。前端必須傳入最近一次查詢取得的 EditTime；成功後以回傳的新 EditTime 更新本地狀態。
    /// 若資料已被他人修改會回傳 409，前端應提示使用者並重新查詢；選項或日期錯誤回傳 400，找不到專案回傳 404。
    /// </remarks>
    [HttpPut("Projects/{projectCode}")]
    [ProducesResponseType(typeof(Result<PrjProjectDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Result<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(Result<object>), StatusCodes.Status409Conflict)]
    public Task<IActionResult> UpdateProject(string projectCode, [FromBody] UpdatePrjProjectRequest request, CancellationToken ct) =>
        ExecuteAsync(() => _service.UpdateProjectAsync(projectCode, request, ct));

    /// <summary>啟用或停用專案。</summary>
    /// <remarks>
    /// 用於列表或編輯頁的啟用開關，屬於軟刪除。Request 的 Enabled 決定目標狀態，EditTime 用於防止覆蓋新資料。
    /// 停用前必須先停用所有啟用中的工作明細，否則回傳 409；成功後使用回傳資料刷新畫面。
    /// </remarks>
    [HttpPatch("Projects/{projectCode}/enabled")]
    [ProducesResponseType(typeof(Result<PrjProjectDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(Result<object>), StatusCodes.Status409Conflict)]
    public Task<IActionResult> ChangeProjectEnabled(string projectCode, [FromBody] ChangeEnabledRequest request, CancellationToken ct) =>
        ExecuteAsync(() => _service.ChangeProjectEnabledAsync(projectCode, request, ct));

    /// <summary>批次調整專案顯示順序。</summary>
    /// <remarks>
    /// 用於專案列表拖拉排序後一次送出。每個項目包含 ProjectCode、Seq 與列表取得的 EditTime；整批在同一交易中執行。
    /// 任一項目被他人修改時回傳 409 並全部回復，前端應重新載入列表。成功回傳 true。
    /// </remarks>
    [HttpPut("Projects/reorder")]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Result<object>), StatusCodes.Status409Conflict)]
    public Task<IActionResult> ReorderProjects([FromBody] ReorderPrjProjectsRequest request, CancellationToken ct) =>
        ExecuteAsync(async () => { await _service.ReorderProjectsAsync(request, ct); return true; });

    /// <summary>分頁查詢指定專案的工作明細。</summary>
    /// <remarks>
    /// 用於專案明細頁、看板或工作清單。前端在切換專案、頁碼、狀態、人員、處理類型、日期及排序時呼叫。
    /// SortBy 支援 Seq、Summary、StartTime、ExpectedTime、EndTime、EditTime；日期中的歷史 1900-01-01 會轉成 null。
    /// 回傳資料已包含狀態名稱、人員名稱、IsCompleted 與 IsOverdue；找不到專案回傳 404。
    /// </remarks>
    [HttpGet("Projects/{projectCode}/Details")]
    [ProducesResponseType(typeof(Result<PagedResult<PrjDetailDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Result<object>), StatusCodes.Status404NotFound)]
    public Task<IActionResult> GetDetails(string projectCode, [FromQuery] PrjDetailQuery query, CancellationToken ct) =>
        ExecuteAsync(() => _service.GetDetailsAsync(projectCode, query, ct));

    /// <summary>取得單筆工作明細。</summary>
    /// <remarks>
    /// 用於工作檢視、編輯對話框或狀態操作前重新整理。detailSid 來自工作列表；修改類 API 必須使用本 API 回傳的最新 EditTime。
    /// 找不到資料回傳 404。
    /// </remarks>
    [HttpGet("Details/{detailSid:decimal}")]
    [ProducesResponseType(typeof(Result<PrjDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result<object>), StatusCodes.Status404NotFound)]
    public Task<IActionResult> GetDetail(decimal detailSid, CancellationToken ct) =>
        ExecuteAsync(() => _service.GetDetailAsync(detailSid, ct));

    /// <summary>在指定專案新增工作明細。</summary>
    /// <remarks>
    /// 用於工作新增表單。StatusNo 必填；處理類型、負責人、支援人員及審核人員應使用 Options／Users API 的值。
    /// 日期傳 ISO 8601 或 null，開始日期不可晚於對應結束日期。成功回傳 201 與新工作；無效專案回傳 404，無效選項或人員回傳 400。
    /// </remarks>
    [HttpPost("Projects/{projectCode}/Details")]
    [ProducesResponseType(typeof(Result<PrjDetailDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(Result<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Result<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateDetail(string projectCode, [FromBody] CreatePrjDetailRequest request, CancellationToken ct)
    {
        try
        {
            var data = await _service.CreateDetailAsync(projectCode, request, ct);
            return CreatedAtAction(nameof(GetDetail), new { detailSid = data.DetailSid }, Result<PrjDetailDto>.Ok(data));
        }
        catch (HttpStatusCodeException ex)
        {
            return BuildError(ex);
        }
    }

    /// <summary>修改工作明細內容。</summary>
    /// <remarks>
    /// 用於工作編輯表單完整儲存。前端應傳回最新 EditTime，成功後以回傳資料更新畫面。
    /// 資料已被修改時回傳 409；無效狀態、處理類型、人員或日期回傳 400；找不到工作回傳 404。
    /// </remarks>
    [HttpPut("Details/{detailSid:decimal}")]
    [ProducesResponseType(typeof(Result<PrjDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Result<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(Result<object>), StatusCodes.Status409Conflict)]
    public Task<IActionResult> UpdateDetail(decimal detailSid, [FromBody] UpdatePrjDetailRequest request, CancellationToken ct) =>
        ExecuteAsync(() => _service.UpdateDetailAsync(detailSid, request, ct));

    /// <summary>單獨變更工作狀態。</summary>
    /// <remarks>
    /// 用於看板拖拉、快速完成或狀態下拉選單，不需送出整份工作資料。StatusNo 必須來自 Options API，EditTime 必須是最新版本。
    /// 成功回傳更新後工作及 IsCompleted／IsOverdue；狀態無效回傳 400，併發衝突回傳 409。
    /// </remarks>
    [HttpPatch("Details/{detailSid:decimal}/status")]
    [ProducesResponseType(typeof(Result<PrjDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Result<object>), StatusCodes.Status409Conflict)]
    public Task<IActionResult> ChangeDetailStatus(decimal detailSid, [FromBody] ChangePrjDetailStatusRequest request, CancellationToken ct) =>
        ExecuteAsync(() => _service.ChangeDetailStatusAsync(detailSid, request, ct));

    /// <summary>啟用或停用工作明細。</summary>
    /// <remarks>
    /// 用於工作清單的軟刪除及還原。Enabled 為目標狀態，EditTime 為目前版本；不會實體刪除資料。
    /// 成功回傳更新後工作，找不到回傳 404，版本不一致回傳 409。
    /// </remarks>
    [HttpPatch("Details/{detailSid:decimal}/enabled")]
    [ProducesResponseType(typeof(Result<PrjDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(Result<object>), StatusCodes.Status409Conflict)]
    public Task<IActionResult> ChangeDetailEnabled(decimal detailSid, [FromBody] ChangeEnabledRequest request, CancellationToken ct) =>
        ExecuteAsync(() => _service.ChangeDetailEnabledAsync(detailSid, request, ct));

    /// <summary>批次調整專案內工作順序。</summary>
    /// <remarks>
    /// 用於工作清單或看板拖拉排序。每個項目帶 DetailSid、Seq、EditTime，且只能更新 route 指定專案內的工作。
    /// 整批交易執行；任一工作不存在、屬於其他專案或版本已變更時回傳 409 並全部回復。
    /// </remarks>
    [HttpPut("Projects/{projectCode}/Details/reorder")]
    [ProducesResponseType(typeof(Result<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Result<object>), StatusCodes.Status409Conflict)]
    public Task<IActionResult> ReorderDetails(string projectCode, [FromBody] ReorderPrjDetailsRequest request, CancellationToken ct) =>
        ExecuteAsync(async () => { await _service.ReorderDetailsAsync(projectCode, request, ct); return true; });

    /// <summary>取得 PRJ 頁面使用的固定選項。</summary>
    /// <remarks>
    /// 用於專案列表與編輯頁初始化，一次取得專案狀態、專案類型、工作狀態及處理類型。
    /// 前端可在進入 PRJ 模組時呼叫並快取；DetailStatuses 的 IsCompleted 可用來顯示完成狀態。此 API 僅讀取，不提供選項維護。
    /// </remarks>
    [HttpGet("Options")]
    [ProducesResponseType(typeof(Result<PrjLookupOptionsDto>), StatusCodes.Status200OK)]
    public Task<IActionResult> GetOptions(CancellationToken ct) => ExecuteAsync(() => _service.GetOptionsAsync(ct));

    /// <summary>搜尋可選客戶。</summary>
    /// <remarks>
    /// 用於專案表單的客戶自動完成欄位。keyword 可省略；take 預設 20、最大 100。輸入文字變更時建議 debounce 後呼叫。
    /// Value 應存入 CustomerNo，Text 僅供顯示；參數範圍錯誤回傳 400。
    /// </remarks>
    [HttpGet("Options/Customers")]
    [ProducesResponseType(typeof(Result<IReadOnlyList<PrjTextOptionDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result<object>), StatusCodes.Status400BadRequest)]
    public Task<IActionResult> GetCustomers([FromQuery] string? keyword, [FromQuery] int take = 20, CancellationToken ct = default) =>
        ExecuteAsync(() => _service.GetCustomersAsync(keyword, take, ct));

    /// <summary>搜尋可選使用者。</summary>
    /// <remarks>
    /// 用於負責人、支援人員及審核人員的自動完成欄位。keyword 可比對帳號或姓名；take 預設 20、最大 100。
    /// Value 是寫入 PRJ_DETAIL 的帳號，Text 是顯示姓名；前端應保存 Value 而非 Text。
    /// </remarks>
    [HttpGet("Options/Users")]
    [ProducesResponseType(typeof(Result<IReadOnlyList<PrjTextOptionDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Result<object>), StatusCodes.Status400BadRequest)]
    public Task<IActionResult> GetUsers([FromQuery] string? keyword, [FromQuery] int take = 20, CancellationToken ct = default) =>
        ExecuteAsync(() => _service.GetUsersAsync(keyword, take, ct));

    private async Task<IActionResult> ExecuteAsync<T>(Func<Task<T>> action)
    {
        try
        {
            return Ok(Result<T>.Ok(await action()));
        }
        catch (HttpStatusCodeException ex)
        {
            return BuildError(ex);
        }
    }

    private IActionResult BuildError(HttpStatusCodeException ex)
    {
        var code = ex.StatusCode switch
        {
            HttpStatusCode.BadRequest => PrjErrorCode.BadRequest,
            HttpStatusCode.Unauthorized => PrjErrorCode.Unauthorized,
            HttpStatusCode.Forbidden => PrjErrorCode.Forbidden,
            HttpStatusCode.NotFound => PrjErrorCode.NotFound,
            HttpStatusCode.Conflict => PrjErrorCode.Conflict,
            _ => PrjErrorCode.UnhandledException
        };
        return StatusCode((int)ex.StatusCode, Result<object>.Fail(code, ex.Message));
    }
}

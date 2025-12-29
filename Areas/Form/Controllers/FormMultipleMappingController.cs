using DcMateH5Api.Areas.Form.Interfaces;
using DcMateH5Api.Areas.Form.Models;
using DcMateH5Api.Areas.Form.ViewModels;
using DcMateH5Api.Helper;
using Microsoft.AspNetCore.Mvc;

namespace DcMateH5Api.Areas.Form.Controllers;

/// <summary>
/// 多對多維護 API，提供左右清單查詢與批次建立/移除關聯。
/// </summary>
[Area("Form")]
[ApiController]
[ApiExplorerSettings(GroupName = SwaggerGroups.FormWithMultipleMapping)]
[Route("[area]/[controller]")]
[Produces("application/json")]
public class FormMultipleMappingController : ControllerBase
{
    private readonly IFormMultipleMappingService _service;

    public FormMultipleMappingController(IFormMultipleMappingService service)
    {
        _service = service;
    }

    /// <summary>
    /// 取得多對多設定檔清單，供前端呈現可選的維護方案。
    /// </summary>
    [HttpGet("masters")]
    [ProducesResponseType(typeof(IEnumerable<MultipleMappingConfigViewModel>), StatusCodes.Status200OK)]
    public ActionResult<IEnumerable<MultipleMappingConfigViewModel>> GetFormMasters(CancellationToken ct)
    {
        var masters = _service.GetFormMasters(ct);
        return Ok(masters);
    }

    /// <summary>
    /// 取得多對多維護可用的主檔資料清單，前端可透過回傳的 Pk 作為 BaseId 呼叫 GetMappingList。
    /// </summary>
    /// <param name="request">查詢條件與分頁設定，需帶入多對多設定檔 FormMasterId。</param>
    /// <param name="ct">取消工作，避免長時間查詢阻塞。</param>
    [HttpPost("search")]
    [ProducesResponseType(typeof(FormListDataViewModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public IActionResult GetForms([FromBody] FormSearchRequest? request, CancellationToken ct)
    {
        if (request == null)
        {
            return BadRequest(new
            {
                Error = "Request body is null",
                Hint = "請確認傳入的 JSON 是否正確，至少需要提供查詢條件或分頁參數"
            });
        }

        var vm = _service.GetForms(request, ct);
        return Ok(vm);
    }
    
    /// <summary>
    /// 依設定檔與主表主鍵取得左右清單（已關聯 / 未關聯）。
    /// </summary>
    /// <param name="formMasterId">多對多設定檔識別碼。</param>
    /// <param name="baseId">主表主鍵值。</param>
    [HttpGet("{formMasterId:guid}/items")]
    [ProducesResponseType(typeof(MultipleMappingListViewModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public IActionResult GetMappingList(Guid formMasterId, [FromQuery] string baseId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(baseId))
        {
            return BadRequest("BaseId 不可為空");
        }

        try
        {
            var result = _service.GetMappingList(formMasterId, baseId, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// 將尚未關聯的明細資料批次加入指定 Base 關聯（右 → 左）。
    /// </summary>
    /// <remarks>
    /// ### 使用說明
    /// 
    /// 此 API 用於將「未關聯 Base」的明細資料，
    /// 批次關聯至指定的 Base 主鍵。
    /// 
    /// - **BaseId**：請傳入上一支 API 回傳的 BasePk
    /// - **DetailIds**：欲關聯的明細主鍵清單
    /// 
    /// 關聯方向為：**Detail → Base（右 → 左）**
    /// 
    /// ### 範例請求
    /// ```json
    /// {
    ///   "BaseId": "98557947437937",
    ///   "DetailIds": [
    ///     "2"
    ///   ]
    /// }
    /// ```
    /// </remarks>
    /// <param name="formMasterId">
    /// 多對多設定檔識別碼。
    /// </param>
    /// <param name="request">
    /// 包含 Base 主鍵與明細主鍵清單的請求模型。
    /// </param>
    [HttpPost("{formMasterId:guid}/items")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public IActionResult AddMappings(
        Guid formMasterId,
        [FromBody] MultipleMappingUpsertViewModel request,
        CancellationToken ct,
        [FromQuery] bool isSeq = false)
    {
        try
        {
            _service.AddMappings(formMasterId, request, isSeq, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }


    /// <summary>
    /// 將已關聯的明細批次移除關聯（左 → 右），BaseId 傳入上上支 api取得的 BasePk
    /// </summary>
    /// <param name="formMasterId">多對多設定檔識別碼。</param>
    /// <param name="request">包含 Base 主鍵與明細主鍵清單的請求模型。</param>
    [HttpPost("{formMasterId:guid}/items/remove")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public IActionResult RemoveMappings(Guid formMasterId, [FromBody] MultipleMappingUpsertViewModel request, CancellationToken ct)
    {
        try
        {
            _service.RemoveMappings(formMasterId, request, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// 依 MAPPING_TABLE_ID 取得關聯表所有資料列，並回傳欄位名稱與對應值。
    /// </summary>
    /// <remarks>
    /// 業務邏輯：
    /// 1. 透過 FormMasterId（對應 FORM_FIELD_MASTER.ID）取得 MAPPING_TABLE_NAME。
    /// 2. 回傳查詢該表全部資料列
    /// </remarks>
    [HttpPost("sequence/reorder")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public IActionResult ReorderSequence([FromBody] MappingSequenceReorderRequest? request, CancellationToken ct)
    {
        if (request == null)
        {
            return BadRequest("請提供排序請求內容。");
        }

        try
        {
            var affected = _service.ReorderMappingSequence(request, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// 依 MAPPING_TABLE_ID 取得關聯表所有資料列，並回傳欄位名稱與對應值。
    /// </summary>
    /// <remarks>
    /// 業務邏輯：
    /// 1. 透過 FormMasterId（對應 FORM_FIELD_MASTER.ID）取得 MAPPING_TABLE_NAME。
    /// 2. 回傳查詢該表全部資料列
    /// </remarks>
    /// <param name="formMasterId">FORM_FIELD_MASTER.ID</param>
    /// <param name="ct">取消權杖。</param>
    [HttpGet("{formMasterId:guid}/mapping-table")]
    [ProducesResponseType(typeof(MappingTableDataViewModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetMappingTableData(Guid formMasterId, CancellationToken ct)
    {
        if (formMasterId == Guid.Empty)
        {
            return BadRequest("FormMasterId 不可為空");
        }

        try
        {
            var result = await _service.GetMappingTableData(formMasterId, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// 依指定的 MAPPING_TABLE_ID 取得關聯資料表的所有資料列，
    /// 並回傳「資料列識別值」及其對應的欄位名稱與欄位值。
    /// </summary>
    /// <remarks>
    /// ### 回傳資料格式說明
    /// 
    /// - **MappingRowId**：關聯表資料列的唯一識別值
    /// - **Fields**：欄位名稱與實際值的 Key-Value 結構
    /// 
    /// ### 範例
    /// ```json
    /// {
    ///   "MappingRowId": "16743049716673",
    ///   "Fields": {
    ///     "ERP_STAGE": "12347"
    ///   }
    /// }
    /// ```
    /// </remarks>
    [HttpPut("{formMasterId:guid}/mapping-table")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateMappingTableData(Guid formMasterId, [FromBody] MappingTableUpdateRequest? request, CancellationToken ct)
    {
        if (formMasterId == Guid.Empty)
        {
            return BadRequest("FormMasterId 不可為空");
        }

        if (request == null)
        {
            return BadRequest("請提供更新內容");
        }

        try
        {
            var affected = await _service.UpdateMappingTableData(formMasterId, request, ct);
            return Ok(new { Affected = affected });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }
}

using ClassLibrary;
using DcMateH5Api.Areas.Form.Interfaces;
using DcMateH5Api.Areas.Form.Models;
using DcMateH5Api.Areas.Form.ViewModels;
using DcMateH5Api.Helper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;

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
    private readonly IFormService _formService;
    private readonly IFormMultipleMappingService _service;
    private readonly FormFunctionType _funcType = FormFunctionType.MultipleMappingMaintenance;

    public FormMultipleMappingController(IFormService formService, IFormMultipleMappingService service)
    {
        _formService = formService;
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
    /// 取得多對多維護的資料列表，回傳 baseTable 內容
    /// </summary>
    /// <remarks>
    /// ### 範例輸入
    /// ```json
    /// [
    ///   {
    ///     "column": "STATUS_CALCD_TIME",
    ///     "ConditionType": 3,
    ///     "value": "2024-12-31",
    ///     "value2": "2025-01-02",
    ///     "dataType": "datetime"
    ///   }
    /// ]
    /// ```
    /// </remarks>
    /// <param name="request">查詢條件與分頁設定</param>
    /// <returns>查詢結果</returns>
    [HttpPost("searchBase")]
    [ProducesResponseType(typeof(FormListDataViewModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public IActionResult GetForms([FromBody] FormSearchRequest? request)
    {
        if (request == null)
        {
            return BadRequest(new
            {
                Error = "Request body is null",
                Hint  = "請確認傳入的 JSON 是否正確，至少需要提供查詢條件或分頁參數"
            });
        }
        
        var vm = _formService.GetFormList( _funcType, request, true );
        return Ok(vm);
    }
    
    /// <summary>
    /// 取得多對多維護的資料列表，回傳 viewTable 內容
    /// </summary>
    /// <remarks>
    /// ### 範例輸入
    /// ```json
    /// [
    ///   {
    ///     "column": "STATUS_CALCD_TIME",
    ///     "ConditionType": 3,
    ///     "value": "2024-12-31",
    ///     "value2": "2025-01-02",
    ///     "dataType": "datetime"
    ///   }
    /// ]
    /// ```
    /// </remarks>
    /// <param name="request">查詢條件與分頁設定</param>
    /// <returns>查詢結果</returns>
    [HttpPost("searchView")]
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
    /// 依設定檔與主表主鍵取得清單（已關聯 / 未關聯），查詢欄位前端動態解析。
    /// </summary>
    /// <param name="formMasterId">多對多設定檔識別碼。</param>
    /// <param name="query">查詢條件，key value</param>
    /// <param name="ct"></param>
    /// <returns></returns>
    [HttpPost("{formMasterId:guid}/items/query")]
    [ProducesResponseType(typeof(MultipleMappingListViewModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public IActionResult GetMappingList(
        Guid formMasterId,
        [FromQuery] MappingListQuery query,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query.BaseId))
            return BadRequest("BaseId 不可為空");

        try
        {
            var result = _service.GetMappingList(
                formMasterId,
                query.BaseId,
                query.Filters,
                query.mode,
                ct);

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
    /// 重新排序指定關聯表（Mapping Table）的顯示順序。
    /// </summary>
    /// <remarks>
    /// ### 說明
    ///
    /// 此 API 用於調整 Mapping Table 中各資料列的排序順序，
    /// 常見使用情境為後台管理介面拖拉排序。
    ///
    /// ### 處理流程
    ///
    /// 1. 依請求中的 `FormMasterId` 取得對應的 Mapping Table 設定。
    /// 2. 驗證欲調整排序的資料列是否存在且屬於該 Mapping Table。
    /// 3. 依請求提供的新順序更新資料列的 Sequence / Sort 欄位。
    ///
    /// ### 注意事項
    ///
    /// - 本操作僅進行排序異動，不回傳資料內容。
    /// - 若請求資料不合法或資料列不存在，將回傳 `400 Bad Request`。
    /// </remarks>
    /// <param name="request">
    /// 排序調整請求，包含目標 Mapping Table 與新的排序順序資訊。
    /// </param>
    /// <param name="ct">CancellationToken</param>
    /// <response code="204">排序更新成功。</response>
    /// <response code="400">請求內容錯誤或排序條件不合法。</response>
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

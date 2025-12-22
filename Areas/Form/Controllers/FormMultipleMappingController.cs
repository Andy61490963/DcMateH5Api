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
    public IActionResult AddMappings(Guid formMasterId, [FromBody] MultipleMappingUpsertViewModel request, CancellationToken ct)
    {
        try
        {
            _service.AddMappings(formMasterId, request, ct);
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
    /// 依 orderedSids 順序重排指定 Base 範圍的 Mapping.SEQ 欄位，
    /// </summary>
    /// <remarks>
    /// ### 使用說明
    /// 
    /// 此 API 會依照前端傳入的 `OrderedSids` 順序，
    /// 重新調整指定 Base 範圍內 Mapping 資料的 `SEQ` 欄位值。
    /// 
    /// ### 範例請求
    /// ```json
    /// {
    ///   "FormMasterId": "5453F7A1-3776-4942-874D-328BCA183CC6",
    ///   "OrderedSids": [
    ///     9944320979279,
    ///     292691828033415
    ///   ],
    ///   "Scope": {
    ///     "BasePkValue": "98557947437937"
    ///   }
    /// }
    /// ```
    /// </remarks>
    /// <returns>
    /// 成功時回傳 204 ；驗證失敗時回傳 400。
    /// </returns>
    [HttpPost("sequence/reorder")]
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
    /// 1. 透過 FormMasterId（對應 FORM_FIELD_MASTER.MAPPING_TABLE_ID）取得 MAPPING_TABLE_NAME。
    /// 2. 驗證表名合法性並確認關聯表欄位存在。
    /// 3. 使用 Dapper 查詢該表全部資料列，逐筆轉為欄位名稱 / 值的結構化模型。
    /// </remarks>
    /// <param name="formMasterId">FORM_FIELD_MASTER.MAPPING_TABLE_ID，指定欲查詢的關聯表設定。</param>
    /// <param name="ct">取消權杖。</param>
    [HttpGet("{formMasterId:guid}/mapping-table")]
    public IActionResult GetMappingTableData(Guid formMasterId, CancellationToken ct)
    {
        if (formMasterId == Guid.Empty)
        {
            return BadRequest("FormMasterId 不可為空");
        }

        try
        {
            var result = _service.GetMappingTableData(formMasterId, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }
}

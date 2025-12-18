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
    /// 取得多對多設定檔清單，供前端呈現可選的維護方案。
    /// </summary>
    [HttpGet("masters")]
    public ActionResult<IEnumerable<MultipleMappingConfigViewModel>> GetFormMasters(CancellationToken ct)
    {
        var masters = _service.GetFormMasters(ct);
        return Ok(masters);
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
    /// 將未關聯的明細批次加入關聯（右 → 左），BaseId 傳入上支 api取得的 BasePk
    /// </summary>
    /// <param name="formMasterId">多對多設定檔識別碼。</param>
    /// <param name="request">包含 Base 主鍵與明細主鍵清單的請求模型。</param>
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
}

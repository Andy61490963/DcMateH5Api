using ClassLibrary;
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
    private readonly IFormService _formService;
    private readonly IFormMultipleMappingService _service;
    private readonly FormFunctionType _funcType = FormFunctionType.MultipleMappingMaintenance;

    private static class Routes
    {
        public const string Masters = "masters";

        public const string SearchBase = "searchBase";
        public const string SearchView = "searchView";

        public const string MappingItemsQuery = "{formMasterId:guid}/items/query";
        public const string MappingItems = "{formMasterId:guid}/items";
        public const string MappingItemsRemove = "{formMasterId:guid}/items/remove";

        public const string SequenceReorder = "sequence/reorder";

        public const string UpdateMappingTable = "{formMasterId:guid}/mapping-table";
    }

    public FormMultipleMappingController(IFormService formService, IFormMultipleMappingService service)
    {
        _formService = formService;
        _service = service;
    }

    /// <summary>
    /// 取得多對多設定檔清單，供前端呈現可選的維護方案。
    /// </summary>
    [HttpGet(Routes.Masters)]
    [ProducesResponseType(typeof(IEnumerable<MultipleMappingConfigViewModel>), StatusCodes.Status200OK)]
    public ActionResult<IEnumerable<MultipleMappingConfigViewModel>> GetFormMasters(CancellationToken ct)
    {
        try
        {
            var masters = _service.GetFormMasters(ct);
            return Ok(masters);
        }
        catch (HttpStatusCodeException ex)
        {
            return StatusCode((int)ex.StatusCode, ex.Message);
        }
    }

    /// <summary>
    /// 取得多對多維護的資料列表，回傳 baseTable 內容
    /// </summary>
    [HttpPost(Routes.SearchBase)]
    [ProducesResponseType(typeof(FormListDataViewModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public IActionResult SearchBaseForms([FromBody] FormSearchRequest? request)
    {
        try
        {
            var badRequest = ValidateSearchRequest(request);
            if (badRequest != null)
            {
                return badRequest;
            }

            var vm = _formService.GetFormList(_funcType, request!, true);
            return Ok(vm);
        }
        catch (HttpStatusCodeException ex)
        {
            return StatusCode((int)ex.StatusCode, ex.Message);
        }
    }

    /// <summary>
    /// 取得多對多維護的資料列表，回傳 viewTable 內容
    /// </summary>
    [HttpPost(Routes.SearchView)]
    [ProducesResponseType(typeof(FormListDataViewModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public IActionResult SearchViewForms([FromBody] FormSearchRequest? request, CancellationToken ct)
    {
        try
        {
            var badRequest = ValidateSearchRequest(request);
            if (badRequest != null)
            {
                return badRequest;
            }

            var vm = _service.GetForms(request!, ct);
            return Ok(vm);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (HttpStatusCodeException ex)
        {
            return StatusCode((int)ex.StatusCode, ex.Message);
        }
    }

    /// <summary>
    /// 依設定檔與主表主鍵取得清單（已關聯 / 未關聯），查詢欄位前端動態解析。
    /// </summary>
    [HttpPost(Routes.MappingItemsQuery)]
    [ProducesResponseType(typeof(MultipleMappingListViewModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public IActionResult GetMappingList(
        [FromRoute] Guid formMasterId,
        [FromQuery] MappingListQuery query,
        CancellationToken ct)
    {
        var badRequest = ValidateMappingListQuery(formMasterId, query);
        if (badRequest != null)
        {
            return badRequest;
        }

        try
        {
            var result = _service.GetMappingList(
                formMasterId,
                query.BaseId!,
                query.Filters,
                query.Type,
                ct);

            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (HttpStatusCodeException ex)
        {
            return StatusCode((int)ex.StatusCode, ex.Message);
        }
    }

    /// <summary>
    /// 將尚未關聯的明細資料批次加入指定 Base 關聯（右 → 左）。
    /// </summary>
    [HttpPost(Routes.MappingItems)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public IActionResult AddMappings(
        [FromRoute] Guid formMasterId,
        [FromBody] MultipleMappingUpsertViewModel? request,
        CancellationToken ct,
        [FromQuery] bool isSeq = false)
    {
        var badRequest = ValidateUpsertMappingsRequest(formMasterId, request);
        if (badRequest != null)
        {
            return badRequest;
        }

        try
        {
            _service.AddMappings(formMasterId, request!, isSeq, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (HttpStatusCodeException ex)
        {
            return StatusCode((int)ex.StatusCode, ex.Message);
        }
    }

    /// <summary>
    /// 將已關聯的明細批次移除關聯（左 → 右）
    /// </summary>
    [HttpPost(Routes.MappingItemsRemove)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public IActionResult RemoveMappings(
        [FromRoute] Guid formMasterId,
        [FromBody] MultipleMappingUpsertViewModel? request,
        CancellationToken ct)
    {
        var badRequest = ValidateUpsertMappingsRequest(formMasterId, request);
        if (badRequest != null)
        {
            return badRequest;
        }

        try
        {
            _service.RemoveMappings(formMasterId, request!, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (HttpStatusCodeException ex)
        {
            return StatusCode((int)ex.StatusCode, ex.Message);
        }
    }

    /// <summary>
    /// 重新排序指定關聯表（Mapping Table）的顯示順序。
    /// </summary>
    [HttpPost(Routes.SequenceReorder)]
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
            _service.ReorderMappingSequence(request, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (HttpStatusCodeException ex)
        {
            return StatusCode((int)ex.StatusCode, ex.Message);
        }
    }

    /// <summary>
    /// 更新 Mapping Table 內容
    /// </summary>
    [HttpPut(Routes.UpdateMappingTable)]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateMappingTableData(
        [FromRoute] Guid formMasterId,
        [FromBody] MappingTableUpdateRequest? request,
        CancellationToken ct)
    {
        var badRequest = ValidateUpdateMappingTableRequest(formMasterId, request);
        if (badRequest != null)
        {
            return badRequest;
        }

        try
        {
            var affected = await _service.UpdateMappingTableData(formMasterId, request!, ct);
            return Ok(new { Affected = affected });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (HttpStatusCodeException ex)
        {
            return StatusCode((int)ex.StatusCode, ex.Message);
        }
    }

    private IActionResult? ValidateSearchRequest(FormSearchRequest? request)
    {
        if (request == null)
        {
            return BadRequest(new
            {
                Error = "Request body is null",
                Hint = "請確認傳入的 JSON 是否正確，至少需要提供查詢條件或分頁參數"
            });
        }

        return null;
    }

    private IActionResult? ValidateMappingListQuery(Guid formMasterId, MappingListQuery query)
    {
        if (formMasterId == Guid.Empty)
        {
            return BadRequest("FormMasterId 不可為空");
        }

        if (string.IsNullOrWhiteSpace(query.BaseId))
        {
            return BadRequest("BaseId 不可為空");
        }

        return null;
    }

    private IActionResult? ValidateUpsertMappingsRequest(Guid formMasterId, MultipleMappingUpsertViewModel? request)
    {
        if (formMasterId == Guid.Empty)
        {
            return BadRequest("FormMasterId 不可為空");
        }

        if (request == null)
        {
            return BadRequest("請提供關聯請求內容。");
        }

        if (string.IsNullOrWhiteSpace(request.BaseId))
        {
            return BadRequest("BaseId 不可為空");
        }

        if (request.DetailIds.Count == 0)
        {
            return BadRequest("DetailIds 不可為空");
        }

        return null;
    }

    private IActionResult? ValidateUpdateMappingTableRequest(Guid formMasterId, MappingTableUpdateRequest? request)
    {
        if (formMasterId == Guid.Empty)
        {
            return BadRequest("FormMasterId 不可為空");
        }

        if (request == null)
        {
            return BadRequest("請提供更新內容");
        }

        return null;
    }
}

using ClassLibrary;
using DcMateH5Api.Areas.Form.Interfaces;
using DcMateH5Api.Areas.Form.Models;
using DcMateH5Api.Areas.Form.ViewModels;
using Microsoft.AspNetCore.Mvc;
using DcMateH5Api.Helper;

namespace DcMateH5Api.Areas.Form.Controllers;

/// <summary>
/// 多對多表單設計 API，提供主檔、目標表與關聯表的欄位設計與表頭設定。
/// </summary>
[Area("Form")]
[ApiController]
[ApiExplorerSettings(GroupName = SwaggerGroups.FormWithMultipleMapping)]
[Route("[area]/[controller]")]
[Produces("application/json")]
public class FormDesignerMultipleMappingController : ControllerBase
{
    private readonly IFormDesignerService _formDesignerService;
    private readonly FormFunctionType _funcType = FormFunctionType.MultipleMappingMaintenance;
    
    /// <summary>
    /// 路由常數集中管理，避免魔法字串散落。
    /// </summary>
    private static class Routes
    {
        // Master
        public const string Root = "";
        public const string FormName = "form-name";
        public const string ById = "{id:guid}";

        // Tables / Fields
        public const string SearchTables = "tables/tableName";
        public const string TableFields = "tables/{tableName}/fields";

        // Header
        public const string Headers = "headers";
    }
    
    public FormDesignerMultipleMappingController(IFormDesignerService formDesignerService)
    {
        _formDesignerService = formDesignerService;
    }

    // ────────── Form Designer 列表 ──────────
    /// <summary>
    /// 取得表單主檔 (FORM_FIELD_MASTER) 清單
    /// </summary>
    /// <param name="q">關鍵字 (模糊搜尋 FORM_NAME)</param>
    /// <param name="ct">CancellationToken</param>
    /// <returns>表單主檔清單</returns>
    [HttpGet(Routes.Root)]
    [ProducesResponseType(typeof(List<FormFieldMasterDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<FormFieldMasterDto>>> GetFormMasters(
        [FromQuery] string? q,
        CancellationToken ct)
    {
        var masters = await _formDesignerService.GetFormMasters(_funcType, q, ct);

        return Ok(masters.ToList());
    }
    
    /// <summary>
    /// 更新主檔 or 明細 or 檢視表 名稱
    /// </summary>
    [HttpPut(Routes.FormName)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateFormName([FromBody] UpdateFormNameViewModel model, CancellationToken ct)
    {
        await _formDesignerService.UpdateFormName(model, ct);   
        return Ok();
    }
    
    /// <summary>
    /// 刪除指定的主檔 or 明細 or 檢視表資料
    /// </summary>
    /// <param name="id">FORM_FIELD_MASTER 的唯一識別編號</param>
    /// <returns>NoContent 回應</returns>
    [HttpDelete(Routes.ById)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public IActionResult Delete(Guid id)
    {
        _formDesignerService.DeleteFormMaster(id);
        return NoContent();
    }
    
    // ────────── Form Designer 入口 ──────────
    /// <summary>
    /// 取得指定的主檔、目標表與關聯表（含檢視表）主畫面資料(請傳入父節點 masterId)
    /// </summary>
    // [RequirePermission(ActionAuthorizeHelper.View)]
    [HttpGet(Routes.ById)]
    [ProducesResponseType(typeof(FormDesignerIndexViewModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDesigner(Guid id, CancellationToken ct)
    {
        var model = await _formDesignerService.GetFormDesignerIndexViewModel(_funcType, id, ct);
        return Ok(model);
    }
    
    // ────────── 欄位相關 ──────────

    /// <summary>
    /// 依名稱關鍵字查詢資料表或檢視表名稱清單(目前列出全部)
    /// 支援前綴與模糊比對（使用 LIKE）。
    /// </summary>
    /// /// <param name="tableName">名稱</param>
    /// <param name="schemaType">欲搜尋的資料來源類型（主表或檢視表）</param>
    /// <returns>符合條件的表名稱集合</returns>
    [HttpGet(Routes.SearchTables)]
    [ProducesResponseType(typeof(List<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public IActionResult SearchTables( string? tableName, [FromQuery] TableSchemaQueryType schemaType )
    {
        var result = _formDesignerService.SearchTables( tableName, schemaType );
        if ( result.Count == 0 ) return NotFound();
        return Ok( result );
    }
    
    /// <summary>
    /// 取得資料表所有欄位設定(tableName必須傳，如果傳入空formMasterId，會創建一筆新的，如果有傳入formMasterId，會取得舊的)
    /// </summary>
    /// <param name="tableName">名稱</param>
    /// <param name="formMasterId">FORM_FIELD_MASTER 的ID</param>
    /// <param name="schemaType">列舉類型</param>
    /// <returns></returns>
    [HttpGet(Routes.TableFields)]
    [ProducesResponseType(typeof(List<FormFieldViewModel>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetFields( string tableName, Guid? formMasterId, [FromQuery] TableSchemaQueryType schemaType )
    {
        var result = await _formDesignerService.EnsureFieldsSaved( tableName, formMasterId, schemaType );
        return Ok( result );
    }
    
  
    
    // ────────── Form Header ──────────
    
    /// <summary>
    /// 儲存多對多表單主檔資訊並建立對應的主 / 目標 / 關聯表設定。
    /// MAPPING_TABLE必須要有 SID(DECIMAL(15,0)) 欄位
    /// </summary>
    [HttpPost(Routes.Headers)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SaveMultipleMappingFormHeader([FromBody] MultipleMappingFormHeaderViewModel model)
    {
        var id = await _formDesignerService.SaveMultipleMappingFormHeader(model);
        return Ok(new { id });
    }
}

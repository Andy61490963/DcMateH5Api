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
    /// <param name="queryType">欲搜尋的資料來源類型（主表或檢視表）</param>
    /// <returns>符合條件的表名稱集合</returns>
    [HttpGet(Routes.SearchTables)]
    [ProducesResponseType(typeof(List<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public IActionResult SearchTables( string? tableName, [FromQuery] TableQueryType queryType )
    {
        var result = _formDesignerService.SearchTables( tableName, queryType );
        if ( result.Count == 0 ) return NotFound();
        return Ok( result );
    }
    
    /// <summary>
    /// 取得資料表所有欄位設定(tableName必須傳，如果傳入空formMasterId，會創建一筆新的，如果有傳入formMasterId，會取得舊的)
    /// </summary>
    /// <param name="tableName">名稱</param>
    /// <param name="formMasterId">FORM_FIELD_MASTER 的ID</param>
    /// <param name="schemaType">列舉類型</param>
    /// <param name="ct"></param>
    /// <returns></returns>
    [HttpGet(Routes.TableFields)]
    [ProducesResponseType(typeof(List<FormFieldViewModel>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetFields( string tableName, Guid? formMasterId, [FromQuery] TableSchemaQueryType schemaType, CancellationToken ct )
    {
        var result = await _formDesignerService.EnsureFieldsSaved( tableName, formMasterId, schemaType, ct );
        return Ok( result );
    }
    
    /// <summary>
    /// 新增或更新單一欄位設定（ID 有值為更新，無值為新增）
    /// </summary>
    /// <param name="model">GetField( Guid fieldId ) 取得的欄位 Json </param>
    /// <returns></returns>
    [HttpPost("fields")]
    [ProducesResponseType(typeof(List<FormFieldViewModel>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpsertField( [FromBody] FormFieldViewModel model )
    {
        try
        {
            if ( model.SchemaType == TableSchemaQueryType.OnlyTable &&
                ( model.QUERY_COMPONENT != null ||
                 model.CAN_QUERY == true ) )
                return Conflict( "無法往主表寫入查詢條件" );
            
            if ( model.SchemaType == TableSchemaQueryType.OnlyTable &&
                ( model.QUERY_DEFAULT_VALUE != null ||
                 model.CAN_QUERY == true ) )
                return Conflict( "無法往主表寫入查詢預設值" );
            
            if ( model.SchemaType == TableSchemaQueryType.OnlyView &&
                ( model.CAN_QUERY == false && model.QUERY_COMPONENT != QueryComponentType.None ) )
                return Conflict( "無法更改未開放查詢條件的查詢元件" );
            
            if ( model.ID != Guid.Empty &&
                _formDesignerService.HasValidationRules( model.ID ) &&
                _formDesignerService.GetControlTypeByFieldId( model.ID ) != model.CONTROL_TYPE )
                return Conflict( "已有驗證規則，無法變更控制元件類型" );

            var master = new FormFieldMasterDto { ID = model.FORM_FIELD_MASTER_ID };
            var formMasterId = _formDesignerService.GetOrCreateFormMasterId( master );

            _formDesignerService.UpsertField( model, formMasterId );
            var fields = await _formDesignerService.GetFieldsByTableName( model.TableName, formMasterId, model.SchemaType );
            return Ok( fields );
        }
        catch ( HttpStatusCodeException ex )
        {
            return StatusCode( (int)ex.StatusCode, ex.Message );
        }
    }
    
    // ────────── Dropdown ──────────

    /// <summary>
    /// 取得下拉選單設定（不存在則自動建立）
    /// </summary>
    /// <param name="dropdownId">FORM_FIELD_DROPDOWN 的ID</param>
    /// <returns></returns>
    [HttpGet("dropdowns/{dropdownId:guid}")]
    [ProducesResponseType(typeof(DropDownViewModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetDropdownSetting( Guid dropdownId )
    {
        var setting = await _formDesignerService.GetDropdownSetting( dropdownId );
        return Ok( setting );
    }

    /// <summary>
    /// 設定下拉選單資料來源模式（SQL/設定檔）
    /// </summary>
    /// <param name="dropdownId">FORM_FIELD_DROPDOWN 的ID</param>
    /// <param name="isUseSql">是否使用Sql當作下拉選單的條件</param>
    /// <param name="ct"></param>
    /// <returns></returns>
    [HttpPut("dropdowns/{dropdownId:guid}/mode")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SetDropdownMode( Guid dropdownId, [FromQuery] bool isUseSql, CancellationToken ct )
    {
        await _formDesignerService.SetDropdownMode( dropdownId, isUseSql, ct );
        return Ok();
    }
    
    /// <summary>
    /// 取得所有下拉選單選項
    /// </summary>
    /// <param name="dropdownId">FORM_FIELD_DROPDOWN 的ID</param>
    /// <param name="ct"></param>
    /// <returns></returns>
    [HttpPost("dropdowns/{dropdownId:guid}/options")]
    [ProducesResponseType(typeof(List<FormFieldDropdownOptionsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetDropdownOption( Guid dropdownId, CancellationToken ct )
    {
        var options = await _formDesignerService.GetDropdownOptions( dropdownId, ct );
        return Ok( options );
    }
    
    /// <summary>
    /// 驗證下拉 SQL 語法
    /// </summary>
    [HttpPost("dropdowns/validate-sql")]
    [ProducesResponseType(typeof(ValidateSqlResultViewModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public IActionResult ValidateDropdownSql( [FromBody] string sql )
    {
        var res = _formDesignerService.ValidateDropdownSql( sql );
        return Ok(res);
    }
    
    /// <summary>
    /// 匯入先前查詢的下拉選單值（僅允許 SELECT，結果需使用 AS NAME）。
    /// </summary>
    /// <param name="dropdownId">FORM_FIELD_DROPDOWN 的ID</param>
    /// <param name="isQueryDropdwon">區分是否為查詢</param>
    /// <param name="dto">SQL 匯入資料</param>
    /// <returns>匯入結果</returns>
    [HttpPost("dropdowns/{dropdownId:guid}/import-previous-query-values")]
    [ProducesResponseType(typeof(PreviousQueryDropdownImportResultViewModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public IActionResult ImportPreviousQueryDropdownValues(Guid dropdownId, bool isQueryDropdwon, [FromBody] ImportOptionViewModel dto)
    {
        var res = _formDesignerService.ImportPreviousQueryDropdownValues(dto.Sql, dropdownId, isQueryDropdwon);
        if (!res.Success)
        {
            return BadRequest(res.Message);
        }

        return Ok(res);
    }
    
    /// <summary>
    /// 使用者自訂的下拉選項，以「前端送來的完整清單」覆蓋下拉選項（Replace All）
    /// </summary>
    /// <remarks>
    /// </remarks>
    [HttpPut("dropdowns/{dropdownId:guid}/options:replace")]
    [ProducesResponseType(typeof(List<FormFieldDropdownOptionsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ReplaceDropdownOptions(
        Guid dropdownId,
        [FromBody] List<DropdownOptionItemViewModel> options,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        await _formDesignerService.ReplaceDropdownOptionsAsync(dropdownId, options, ct);

        var latestOptions = await _formDesignerService.GetDropdownOptions(dropdownId, ct);
        return Ok(latestOptions);
    }
    
    /// <summary>
    /// 移動表單欄位的顯示順序（使用 分數索引排序 演算法）。
    /// </summary>
    /// <remarks>
    /// ### 使用方式
    /// 
    /// 此 API 用於「拖拉排序」或「指定位置移動」欄位，
    /// **只會更新指定欄位的排序 Key，不會重排其他欄位**。
    /// 
    /// 排序邏輯說明：
    /// - 系統使用 分數索引 產生排序 Key
    /// - 排序值不保證連續（例如：1000、2500、3000）
    ///   僅保證大小關係正確，可有效避免大量更新資料。
    /// 
    /// ### Request Body 說明
    /// 
    /// ```json
    /// {
    ///   "movingId": "要移動的欄位 ID",
    ///   "prevId": "移動後前一個欄位 ID（放最前請傳 null）",
    ///   "nextId": "移動後後一個欄位 ID（放最後請傳 null）"
    /// }
    /// ```
    /// 
    /// - **movingId**：
    ///   必填，要移動的 `FORM_FIELD_CONFIG.ID`。
    /// 
    /// - **prevId / nextId**：
    ///   - 代表「移動後」的位置，而非移動前。
    ///   - `prevId = null` 表示移動至最前方。
    ///   - `nextId = null` 表示移動至最後方。
    /// 
    /// ### 範例
    /// 
    /// 將欄位 C 移動至欄位 A 與 B 之間：
    /// 
    /// ```json
    /// {
    ///   "movingId": "C",
    ///   "prevId": "A",
    ///   "nextId": "B"
    /// }
    /// ```
    /// 
    /// </remarks>
    /// <param name="req">欄位排序移動請求內容</param>
    /// <param name="ct">取消權杖</param>
    /// <returns>成功時回傳 HTTP 200</returns>
    [HttpPost("fields/move")]
    public async Task<IActionResult> MoveField(
        [FromBody] MoveFormFieldRequest req,
        CancellationToken ct)
    {
        await _formDesignerService.MoveFieldAsync(req, ct);
        return Ok();
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

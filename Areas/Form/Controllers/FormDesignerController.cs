using ClassLibrary;
using DcMateH5Api.Areas.Form.Models;
using DcMateH5Api.Areas.Form.Interfaces;
using DcMateH5Api.Areas.Form.ViewModels;
using Microsoft.AspNetCore.Mvc;
using DcMateH5Api.Helper;
using DcMateH5Api.Controllers;

namespace DcMateH5Api.Areas.Form.Controllers;

[Area("Form")]
[ApiController]
[ApiExplorerSettings(GroupName = SwaggerGroups.Form)]
[Route("[area]/[controller]")]
public class FormDesignerController : BaseController
{
    private readonly IFormDesignerService _formDesignerService;
    private readonly FormFunctionType _funcType = FormFunctionType.MasterMaintenance;
    
    public FormDesignerController(
        IFormDesignerService formDesignerService)
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
    [HttpGet]
    [ProducesResponseType(typeof(List<FormFieldMasterDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<FormFieldMasterDto>>> GetFormMasters( [FromQuery] string? q, CancellationToken ct )
    {
        var masters = await _formDesignerService.GetFormMasters( _funcType, q, ct );
        return Ok( masters.ToList() );
    }
    
    /// <summary>
    /// 更新主檔 or 明細 or 檢視表 名稱
    /// </summary>
    /// <param name="model"></param>
    /// <param name="ct">CancellationToken</param>
    /// <returns></returns>
    [HttpPut("form-name")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateFormName( [FromBody] UpdateFormNameViewModel model, CancellationToken ct )
    {
        await _formDesignerService.UpdateFormName( model, ct );
        return Ok();
    }
    
    /// <summary>
    /// 刪除指定的主檔或明細或檢視表資料
    /// </summary>
    /// <param name="id">FORM_FIELD_MASTER 的ID</param>
    /// <returns>NoContent 回應</returns>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Delete( Guid id )
    {
        await _formDesignerService.DeleteFormMaster( id );
        return NoContent();
    }
    
    // ────────── Form Designer 入口 ──────────
    /// <summary>
    /// 取得指定的 主檔、檢視表 主畫面資料(請傳入父節點 masterId)
    /// </summary>
    /// <param name="id">FORM_FIELD_MASTER 的ID</param>
    /// <param name="ct"></param>
    /// <returns></returns>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(FormDesignerIndexViewModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDesigner( Guid id, CancellationToken ct )
    {
        var model = await _formDesignerService.GetFormDesignerIndexViewModel( _funcType, id, ct );
        return Ok( model );
    }

    // ────────── 欄位相關 ──────────

    /// <summary>
    /// 依名稱關鍵字查詢資料表或檢視表名稱清單(目前列出全部)
    /// 支援前綴與模糊比對（使用 LIKE）。
    /// </summary>
    /// /// <param name="tableName">名稱</param>
    /// <param name="schemaType">欲搜尋的資料來源類型（主表或檢視表）</param>
    /// <returns>符合條件的表名稱集合</returns>
    [HttpGet("tables/tableName")]
    [ProducesResponseType(typeof(List<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public IActionResult SearchTables( string? tableName, [FromQuery] TableSchemaQueryType schemaType )
    {
        try
        {
            var result = _formDesignerService.SearchTables( tableName, schemaType );
            if ( result.Count == 0 ) return NotFound();
            return Ok( result );
        }
        catch ( HttpStatusCodeException ex )
        {
            return StatusCode( (int)ex.StatusCode, ex.Message );
        }
    }
    
    /// <summary>
    /// 取得資料表所有欄位設定(tableName必須傳，如果傳入空formMasterId，會創建一筆新的，如果有傳入formMasterId，會取得舊的)
    /// </summary>
    /// <param name="tableName">名稱</param>
    /// <param name="formMasterId">FORM_FIELD_MASTER 的ID</param>
    /// <param name="schemaType">列舉類型</param>
    /// <returns></returns>
    [HttpGet("tables/{tableName}/fields")]
    [ProducesResponseType(typeof(List<FormFieldViewModel>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetFields( string tableName, Guid? formMasterId, [FromQuery] TableSchemaQueryType schemaType )
    {
        try
        {
            var result = await _formDesignerService.EnsureFieldsSaved( tableName, formMasterId, schemaType );

            if ( result == null ) return NotFound();
            return Ok( result );
        }
        catch ( HttpStatusCodeException ex )
        {
            return StatusCode( (int)ex.StatusCode, ex.Message );
        }
    }

    /// <summary>
    /// 依欄位設定 ID 取得單一欄位設定 ( GetFields搜尋時就會先預先建立完成 )
    /// </summary>
    /// <param name="fieldId">FORM_FIELD_CONFIG 的ID</param>
    /// <returns></returns>
    [HttpGet("fields/{fieldId}")]
    [ProducesResponseType(typeof(FormFieldViewModel), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetField( Guid fieldId )
    {
        var field = await _formDesignerService.GetFieldById( fieldId );
        if ( field == null ) return NotFound();
        return Ok( field );
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
                ( model.QUERY_COMPONENT != QueryComponentType.None ||
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

    // ────────── 批次設定 ──────────

    /// <summary>
    /// 批次設定所有欄位為可編輯/不可編輯
    /// </summary>
    /// <param name="formMasterId">FORM_FIELD_MASTER 的ID</param>
    /// <param name="isEditable">是否可編輯</param>
    /// <param name="ct"></param>
    /// <returns></returns>
    [HttpPost("tables/fields/batch-editable")]
    [ProducesResponseType(typeof(List<FormFieldListViewModel>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> BatchSetEditable( [FromQuery] Guid formMasterId, [FromQuery] bool isEditable, CancellationToken ct )
    {
        var model = await _formDesignerService.SetAllEditable( formMasterId, isEditable, ct );
        var fields = await _formDesignerService.GetFieldsByTableName( model, formMasterId, TableSchemaQueryType.OnlyTable );
        return Ok( fields );
    }

    /// <summary>
    /// 批次設定所有欄位為必填/非必填
    /// </summary>
    /// <param name="formMasterId">FORM_FIELD_MASTER 的ID</param>
    /// <param name="isRequired">是否必填</param>
    /// <param name="ct"></param>
    /// <returns></returns>
    [HttpPost("tables/fields/batch-required")]
    [ProducesResponseType(typeof(List<FormFieldListViewModel>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> BatchSetRequired( [FromQuery] Guid formMasterId, [FromQuery] bool isRequired, CancellationToken ct )
    {
        var tableName = await _formDesignerService.SetAllRequired( formMasterId, isRequired, ct );
        var fields = await _formDesignerService.GetFieldsByTableName( tableName, formMasterId,  TableSchemaQueryType.OnlyTable );
        return Ok( fields );
    }

    // ────────── 欄位驗證規則 ──────────

    /// <summary>
    /// 新增一筆空的驗證規則並回傳全部規則
    /// </summary>
    /// <param name="fieldId">FORM_FIELD_CONFIG 的ID</param>
    /// <param name="ct"></param>
    /// <returns></returns>
    [HttpPost("fields/{fieldId:guid}/rules")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AddEmptyValidationRule( Guid fieldId, CancellationToken ct = default )
    {
        var rule = _formDesignerService.CreateEmptyValidationRule( fieldId );
        await _formDesignerService.InsertValidationRule(rule);
        var rules = await _formDesignerService.GetValidationRulesByFieldId( fieldId, ct );
        return Ok( new { rules } );
    }
    
    /// <summary>
    /// 取得欄位驗證規則與驗證類型選項
    /// </summary>
    /// <param name="fieldId">FORM_FIELD_CONFIG 的ID</param>
    /// <param name="ct"></param>
    /// <returns></returns>
    [HttpGet("fields/{fieldId:guid}/rules")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetValidationRules( Guid fieldId, CancellationToken ct = default )
    {
        if ( fieldId == Guid.Empty )
            return BadRequest("請先設定控制元件後再新增驗證條件。");
        
        var rules = await _formDesignerService.GetValidationRulesByFieldId( fieldId, ct );
        return Ok( new { rules } );   
    }

    /// <summary>
    /// 更新單一驗證規則
    /// </summary>
    /// <param name="model"></param>
    /// <returns></returns>
    [HttpPut("rules")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateValidationRule( [FromBody] FormFieldValidationRuleDto model )
    {
        await _formDesignerService.SaveValidationRule( model );
        return Ok();
    }

    /// <summary>
    /// 刪除驗證規則
    /// </summary>
    /// <param name="id">FORM_FIELD_VALIDATION_RULE 的ID</param>
    /// <returns></returns>
    [HttpDelete("rules/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeleteValidationRule( Guid id )
    {
        await _formDesignerService.DeleteValidationRule( id );
        // var rules = _formDesignerService.GetValidationRulesByFieldId( fieldConfigId );
        return NoContent();
    }

    // ────────── Dropdown ──────────

    /// <summary>
    /// 取得下拉選單設定（不存在則自動建立）
    /// </summary>
    /// <param name="fieldId">FORM_FIELD_CONFIG 的ID</param>
    /// <returns></returns>
    [HttpGet("fields/{fieldId:guid}/dropdown")]
    [ProducesResponseType(typeof(DropDownViewModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetDropdownSetting( Guid fieldId )
    {
        var field = await _formDesignerService.GetFieldById( fieldId );
        if ( field == null )
        {
            return BadRequest( "查無此設定檔，請確認ID是否正確。" );
        }
        // if (field.SchemaType != TableSchemaQueryType.OnlyTable)
        // {
        //     return BadRequest( "下拉選單設定只支援主擋。" );
        // }
        _formDesignerService.EnsureDropdownCreated( fieldId );
        var setting = await _formDesignerService.GetDropdownSetting( fieldId );
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
    [HttpPost("dropdowns/{dropdownId:guid}")]
    [ProducesResponseType(typeof(List<FormFieldDropdownOptionsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetDropdownOption( Guid dropdownId, CancellationToken ct )
    {
        var options = await _formDesignerService.GetDropdownOptions( dropdownId, ct );
        return Ok( options );
    }
    
    // /// <summary>
    // /// 儲存下拉選單 SQL 查詢
    // /// </summary>
    // /// <param name="dropdownId">FORM_FIELD_DROPDOWN 的ID</param>
    // /// <param name="sql">使用Sql當作下拉選單的條件，Sql的內容</param>
    // /// <param name="ct"></param>
    // /// <returns></returns>
    // [HttpPut("dropdowns/{dropdownId:guid}/sql")]
    // public async Task<IActionResult> SaveDropdownSql( Guid dropdownId, [FromBody] string sql, CancellationToken ct )
    // {
    //     await _formDesignerService.SaveDropdownSql( dropdownId, sql, ct );
    //     return Ok();
    // }

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
    /// 匯入下拉選單選項（由 SQL 查詢）
    /// </summary>
    /// <param name="dropdownId">FORM_FIELD_DROPDOWN 的ID</param>
    /// <param name="dto"></param>
    /// <returns></returns>
    [HttpPost("dropdowns/{dropdownId:guid}/import-options")]
    [ProducesResponseType(typeof(List<FormFieldDropdownOptionsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ImportDropdownOptions( Guid dropdownId, [FromBody] ImportOptionViewModel dto )
    {
        var res = _formDesignerService.ImportDropdownOptionsFromSql( dto.Sql, dropdownId );
        if ( !res.Success ) return BadRequest( res.Message );

        var options = await _formDesignerService.GetDropdownOptions( dropdownId );
        return Ok( options );
    }

    /// <summary>
    /// 匯入先前查詢的下拉選單值（僅允許 SELECT，結果需使用 AS NAME）。
    /// </summary>
    /// <param name="dropdownId">FORM_FIELD_DROPDOWN 的ID</param>
    /// <param name="dto">SQL 匯入資料</param>
    /// <returns>匯入結果</returns>
    [HttpPost("dropdowns/{dropdownId:guid}/import-previous-query-values")]
    [ProducesResponseType(typeof(PreviousQueryDropdownImportResultViewModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public IActionResult ImportPreviousQueryDropdownValues(Guid dropdownId, [FromBody] ImportOptionViewModel dto)
    {
        var res = _formDesignerService.ImportPreviousQueryDropdownValues(dto.Sql, dropdownId);
        if (!res.Success)
        {
            return BadRequest(res.Message);
        }

        return Ok(res);
    }

    /// <summary>
    /// 建立一筆空白下拉選項
    /// </summary>
    /// <param name="dropdownId">FORM_FIELD_DROPDOWN 的ID</param>
    /// <param name="ct"></param>
    /// <returns></returns>
    [HttpPost("dropdowns/{dropdownId:guid}/options")]
    [ProducesResponseType(typeof(List<FormFieldDropdownOptionsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateDropdownOption( Guid dropdownId, CancellationToken ct )
    {
        _formDesignerService.SaveDropdownOption( null, dropdownId, "", "" );
        var options = await _formDesignerService.GetDropdownOptions( dropdownId, ct );
        return Ok( options );
    }

    /// <summary>
    /// 儲存單筆下拉選項（新增/更新）
    /// </summary>
    /// <param name="dropdownId">FORM_FIELD_DROPDOWN 的ID</param>
    /// <param name="dto"></param>
    /// <returns></returns>
    [HttpPut("dropdowns/{dropdownId:guid}/options")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public IActionResult SaveDropdownOption( Guid dropdownId, [FromBody] SaveOptionViewModel dto )
    {
        _formDesignerService.SaveDropdownOption( dto.Id, dropdownId, dto.OptionText, dto.OptionValue );
        return Ok();
    }

    /// <summary>
    /// 刪除下拉選項
    /// </summary>
    /// <param name="optionId">FORM_FIELD_DROPDOWN_OPTIONS 的ID</param>
    /// <param name="dropdownId"></param>
    /// <returns></returns>
    [HttpDelete("dropdowns/options/{optionId:guid}")]
    [ProducesResponseType(typeof(List<FormFieldDropdownOptionsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeleteDropdownOption( Guid optionId, [FromQuery] Guid dropdownId )
    {
        await _formDesignerService.DeleteDropdownOption( optionId );
        var options = await _formDesignerService.GetDropdownOptions( dropdownId );
        return Ok(options);
    }

    // ────────── 刪除防呆 SQL ──────────

    /// <summary>
    /// 取得刪除防呆 SQL 規則清單（可依表單主檔 ID 篩選）。
    /// </summary>
    /// <param name="formFieldMasterId">FORM_FIELD_MASTER 的ID</param>
    /// <param name="ct">CancellationToken</param>
    /// <returns>刪除防呆 SQL 規則清單</returns>
    [HttpGet("delete-guard-sqls")]
    [ProducesResponseType(typeof(List<FormFieldDeleteGuardSqlDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<FormFieldDeleteGuardSqlDto>>> GetDeleteGuardSqls(
        [FromQuery] Guid? formFieldMasterId,
        CancellationToken ct)
    {
        var rules = await _formDesignerService.GetDeleteGuardSqls(formFieldMasterId, ct);
        return Ok(rules);
    }

    /// <summary>
    /// 取得單筆刪除防呆 SQL 規則。
    /// </summary>
    /// <param name="id">規則 ID</param>
    /// <param name="ct">CancellationToken</param>
    /// <returns>刪除防呆 SQL 規則</returns>
    [HttpGet("delete-guard-sqls/{id:guid}")]
    [ProducesResponseType(typeof(FormFieldDeleteGuardSqlDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDeleteGuardSql(Guid id, CancellationToken ct)
    {
        var rule = await _formDesignerService.GetDeleteGuardSql(id, ct);
        if (rule == null) return NotFound();
        return Ok(rule);
    }

    /// <summary>
    /// 新增刪除防呆 SQL 規則。
    /// </summary>
    /// <param name="model">新增資料</param>
    /// <param name="ct">CancellationToken</param>
    /// <returns>新增後的刪除防呆 SQL 規則</returns>
    [HttpPost("delete-guard-sqls")]
    [ProducesResponseType(typeof(FormFieldDeleteGuardSqlDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<FormFieldDeleteGuardSqlDto>> CreateDeleteGuardSql(
        [FromBody] FormFieldDeleteGuardSqlCreateViewModel model,
        CancellationToken ct)
    {
        var userId = CurrentUser.IsAuthenticated ? CurrentUser.Id : (Guid?)null;
        var rule = await _formDesignerService.CreateDeleteGuardSql(model, userId, ct);
        return Ok(rule);
    }

    /// <summary>
    /// 更新刪除防呆 SQL 規則。
    /// </summary>
    /// <param name="id">規則 ID</param>
    /// <param name="model">更新資料</param>
    /// <param name="ct">CancellationToken</param>
    /// <returns>更新後的刪除防呆 SQL 規則</returns>
    [HttpPut("delete-guard-sqls/{id:guid}")]
    [ProducesResponseType(typeof(FormFieldDeleteGuardSqlDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateDeleteGuardSql(
        Guid id,
        [FromBody] FormFieldDeleteGuardSqlUpdateViewModel model,
        CancellationToken ct)
    {
        var userId = CurrentUser.IsAuthenticated ? CurrentUser.Id : (Guid?)null;
        var rule = await _formDesignerService.UpdateDeleteGuardSql(id, model, userId, ct);
        if (rule == null) return NotFound();
        return Ok(rule);
    }

    /// <summary>
    /// 刪除刪除防呆 SQL 規則
    /// </summary>
    /// <param name="id">規則 ID</param>
    /// <param name="ct">CancellationToken</param>
    /// <returns>NoContent 回應</returns>
    [HttpDelete("delete-guard-sqls/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteDeleteGuardSql(Guid id, CancellationToken ct)
    {
        var userId = CurrentUser.IsAuthenticated ? CurrentUser.Id : (Guid?)null;
        var deleted = await _formDesignerService.DeleteDeleteGuardSql(id, userId, ct);
        if (!deleted) return NotFound();
        return NoContent();
    }
    
    // ────────── Form Header ──────────

    /// <summary>
    /// 儲存表單主檔資訊
    /// </summary>
    /// <param name="model"></param>
    /// <returns></returns>
    [HttpPost("headers")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SaveFormHeader( [FromBody] FormHeaderViewModel model )
    {
        if ( model.BASE_TABLE_ID == Guid.Empty || model.VIEW_TABLE_ID == Guid.Empty )
            return BadRequest("BASE_TABLE_ID / VIEW_TABLE_ID 不可為空");

        // if ( _formDesignerService.CheckFormMasterExists( model.BASE_TABLE_ID, model.VIEW_TABLE_ID, model.ID ) )
        //     return Conflict("相同的表格及 View 組合已存在");

        var id = await _formDesignerService.SaveFormHeader( model );
        return Ok( new { id } );
    }
}

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
    [HttpGet]
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
    [HttpPut("form-name")]
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
    [HttpDelete("{id}")]
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
    [HttpGet("{id:guid}")]
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
    [HttpGet("tables/tableName")]
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
    // [HttpGet("fields/{fieldId}")]
    // public async Task<IActionResult> GetField( Guid fieldId )
    // {
    //     var field = await _formDesignerService.GetFieldById( fieldId );
    //     if ( field == null ) return NotFound();
    //     return Ok( field );
    // }
    
    // /// <summary>
    // /// 新增或更新單一欄位設定（ID 有值為更新，無值為新增）
    // /// </summary>
    // /// <param name="model">GetField( Guid fieldId ) 取得的欄位 Json </param>
    // /// <returns></returns>
    // [HttpPost("fields")]
    // public async Task<IActionResult> UpsertField( [FromBody] FormFieldViewModel model )
    // {
    //     try
    //     {
    //         if ( model.SchemaType == TableSchemaQueryType.OnlyTable &&
    //              ( model.QUERY_COMPONENT != QueryComponentType.None ||
    //                model.CAN_QUERY == true ) )
    //             return Conflict( "無法往主表寫入查詢條件" );
    //         
    //         if ( model.SchemaType == TableSchemaQueryType.OnlyTable &&
    //              ( model.QUERY_DEFAULT_VALUE != null ||
    //                model.CAN_QUERY == true ) )
    //             return Conflict( "無法往主表寫入查詢預設值" );
    //         
    //         if ( model.SchemaType == TableSchemaQueryType.OnlyView &&
    //              ( model.CAN_QUERY == false && model.QUERY_COMPONENT != QueryComponentType.None ) )
    //             return Conflict( "無法更改未開放查詢條件的查詢元件" );
    //         
    //         if ( model.ID != Guid.Empty &&
    //              _formDesignerService.HasValidationRules( model.ID ) &&
    //              _formDesignerService.GetControlTypeByFieldId( model.ID ) != model.CONTROL_TYPE )
    //             return Conflict( "已有驗證規則，無法變更控制元件類型" );
    //
    //         var master = new FormFieldMasterDto { ID = model.FORM_FIELD_MASTER_ID };
    //         var formMasterId = _formDesignerService.GetOrCreateFormMasterId( master );
    //
    //         _formDesignerService.UpsertField( model, formMasterId );
    //         var fields = await _formDesignerService.GetFieldsByTableName( model.TableName, formMasterId, model.SchemaType );
    //         return Ok( fields );
    //     }
    //     catch ( HttpStatusCodeException ex )
    //     {
    //         return StatusCode( (int)ex.StatusCode, ex.Message );
    //     }
    // }
    
    // // ────────── 批次設定 ──────────
    //
    // /// <summary>
    // /// 批次設定所有欄位為可編輯/不可編輯
    // /// </summary>
    // /// <param name="formMasterId">FORM_FIELD_MASTER 的ID</param>
    // /// <param name="isEditable">是否可編輯</param>
    // /// <param name="ct"></param>
    // /// <returns></returns>
    // [HttpPost("tables/fields/batch-editable")]
    // public async Task<IActionResult> BatchSetEditable( [FromQuery] Guid formMasterId, [FromQuery] bool isEditable, CancellationToken ct )
    // {
    //     var model = await _formDesignerService.SetAllEditable( formMasterId, isEditable, ct );
    //     var fields = await _formDesignerService.GetFieldsByTableName( model, formMasterId, TableSchemaQueryType.OnlyTable );
    //     return Ok( fields );
    // }
    //
    // /// <summary>
    // /// 批次設定所有欄位為必填/非必填
    // /// </summary>
    // /// <param name="formMasterId">FORM_FIELD_MASTER 的ID</param>
    // /// <param name="isRequired">是否必填</param>
    // /// <param name="ct"></param>
    // /// <returns></returns>
    // [HttpPost("tables/fields/batch-required")]
    // public async Task<IActionResult> BatchSetRequired( [FromQuery] Guid formMasterId, [FromQuery] bool isRequired, CancellationToken ct )
    // {
    //     var tableName = await _formDesignerService.SetAllRequired( formMasterId, isRequired, ct );
    //     var fields = await _formDesignerService.GetFieldsByTableName( tableName, formMasterId,  TableSchemaQueryType.OnlyTable );
    //     return Ok( fields );
    // }
    //
    // // ────────── 欄位驗證規則 ──────────
    //
    // /// <summary>
    // /// 新增一筆空的驗證規則並回傳全部規則
    // /// </summary>
    // /// <param name="fieldId">FORM_FIELD_CONFIG 的ID</param>
    // /// <param name="ct"></param>
    // /// <returns></returns>
    // [HttpPost("fields/{fieldId:guid}/rules")]
    // public async Task<IActionResult> AddEmptyValidationRule( Guid fieldId, CancellationToken ct = default )
    // {
    //     var rule = _formDesignerService.CreateEmptyValidationRule( fieldId );
    //     await _formDesignerService.InsertValidationRule( rule, ct );
    //     var rules = await _formDesignerService.GetValidationRulesByFieldId( fieldId, ct );
    //     return Ok( new { rules } );
    // }
    //
    // /// <summary>
    // /// 取得欄位驗證規則與驗證類型選項
    // /// </summary>
    // /// <param name="fieldId">FORM_FIELD_CONFIG 的ID</param>
    // /// <param name="ct"></param>
    // /// <returns></returns>
    // [HttpGet("fields/{fieldId:guid}/rules")]
    // public async Task<IActionResult> GetValidationRules( Guid fieldId, CancellationToken ct = default )
    // {
    //     if ( fieldId == Guid.Empty )
    //         return BadRequest("請先設定控制元件後再新增驗證條件。");
    //     
    //     var rules = await _formDesignerService.GetValidationRulesByFieldId( fieldId, ct );
    //     return Ok( new { rules } );   
    // }
    //
    // /// <summary>
    // /// 更新單一驗證規則
    // /// </summary>
    // /// <param name="model"></param>
    // /// <returns></returns>
    // [HttpPut("rules")]
    // public async Task<IActionResult> UpdateValidationRule( [FromBody] FormFieldValidationRuleDto model )
    // {
    //     await _formDesignerService.SaveValidationRule( model );
    //     return Ok();
    // }
    //
    // /// <summary>
    // /// 刪除驗證規則
    // /// </summary>
    // /// <param name="id">FORM_FIELD_VALIDATION_RULE 的ID</param>
    // /// <returns></returns>
    // [HttpDelete("rules/{id:guid}")]
    // public async Task<IActionResult> DeleteValidationRule( Guid id )
    // {
    //     await _formDesignerService.DeleteValidationRule( id );
    //     // var rules = _formDesignerService.GetValidationRulesByFieldId( fieldConfigId );
    //     return NoContent();
    // }
    //
    // // ────────── Dropdown ──────────
    //
    // /// <summary>
    // /// 取得下拉選單設定（不存在則自動建立）
    // /// </summary>
    // /// <param name="fieldId">FORM_FIELD_CONFIG 的ID</param>
    // /// <returns></returns>
    // [HttpGet("fields/{fieldId:guid}/dropdown")]
    // public async Task<IActionResult> GetDropdownSetting( Guid fieldId )
    // {
    //     var field = await _formDesignerService.GetFieldById( fieldId );
    //     if ( field == null )
    //     {
    //         return BadRequest( "查無此設定檔，請確認ID是否正確。" );
    //     }
    //     if (field.SchemaType != TableSchemaQueryType.OnlyTable && field.SchemaType != TableSchemaQueryType.OnlyDetail)
    //     {
    //         return BadRequest( "下拉選單設定只支援主擋、明細。" );
    //     }
    //     _formDesignerService.EnsureDropdownCreated( fieldId );
    //     var setting = await _formDesignerService.GetDropdownSetting( fieldId );
    //     return Ok( setting );
    // }
    //
    // /// <summary>
    // /// 設定下拉選單資料來源模式（SQL/設定檔）
    // /// </summary>
    // /// <param name="dropdownId">FORM_FIELD_DROPDOWN 的ID</param>
    // /// <param name="isUseSql">是否使用Sql當作下拉選單的條件</param>
    // /// <param name="ct"></param>
    // /// <returns></returns>
    // [HttpPut("dropdowns/{dropdownId:guid}/mode")]
    // public async Task<IActionResult> SetDropdownMode( Guid dropdownId, [FromQuery] bool isUseSql, CancellationToken ct )
    // {
    //     await _formDesignerService.SetDropdownMode( dropdownId, isUseSql, ct );
    //     return Ok();
    // }
    //
    // /// <summary>
    // /// 取得所有下拉選單選項(排除Sql)
    // /// </summary>
    // /// <param name="dropdownId">FORM_FIELD_DROPDOWN 的ID</param>
    // /// <param name="ct"></param>
    // /// <returns></returns>
    // [HttpPost("dropdowns/{dropdownId:guid}")]
    // public async Task<IActionResult> GetDropdownOption( Guid dropdownId, CancellationToken ct )
    // {
    //     var options = await _formDesignerService.GetDropdownOptions( dropdownId, ct );
    //     return Ok( options );
    // }
    
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

    // /// <summary>
    // /// 驗證下拉 SQL 語法
    // /// </summary>
    // [HttpPost("dropdowns/validate-sql")]
    // public IActionResult ValidateDropdownSql( [FromBody] string sql )
    // {
    //     var res = _formDesignerService.ValidateDropdownSql( sql );
    //     return Ok(res);
    // }
    //
    // /// <summary>
    // /// 匯入下拉選單選項（由 SQL 查詢）
    // /// </summary>
    // /// <param name="dropdownId">FORM_FIELD_DROPDOWN 的ID</param>
    // /// <param name="dto"></param>
    // /// <returns></returns>
    // [HttpPost("dropdowns/{dropdownId:guid}/import-options")]
    // public async Task<IActionResult> ImportDropdownOptions( Guid dropdownId, [FromBody] ImportOptionViewModel dto )
    // {
    //     var res = _formDesignerService.ImportDropdownOptionsFromSql( dto.Sql, dropdownId );
    //     if ( !res.Success ) return BadRequest( res.Message );
    //
    //     var options = await _formDesignerService.GetDropdownOptions( dropdownId );
    //     return Ok( options );
    // }
    //
    // /// <summary>
    // /// 建立一筆空白下拉選項
    // /// </summary>
    // /// <param name="dropdownId">FORM_FIELD_DROPDOWN 的ID</param>
    // /// <param name="ct"></param>
    // /// <returns></returns>
    // [HttpPost("dropdowns/{dropdownId:guid}/options")]
    // public async Task<IActionResult> CreateDropdownOption( Guid dropdownId, CancellationToken ct )
    // {
    //     _formDesignerService.SaveDropdownOption( null, dropdownId, "", "" );
    //     var options = await _formDesignerService.GetDropdownOptions( dropdownId, ct );
    //     return Ok( options );
    // }
    //
    // /// <summary>
    // /// 儲存單筆下拉選項（新增/更新）
    // /// </summary>
    // /// <param name="dropdownId">FORM_FIELD_DROPDOWN 的ID</param>
    // /// <param name="dto"></param>
    // /// <returns></returns>
    // [HttpPut("dropdowns/{dropdownId:guid}/options")]
    // public IActionResult SaveDropdownOption( Guid dropdownId, [FromBody] SaveOptionViewModel dto )
    // {
    //     _formDesignerService.SaveDropdownOption( dto.Id, dropdownId, dto.OptionText, dto.OptionValue );
    //     return Ok();
    // }
    //
    // /// <summary>
    // /// 刪除下拉選項
    // /// </summary>
    // /// <param name="optionId">FORM_FIELD_DROPDOWN_OPTIONS 的ID</param>
    // /// <param name="dropdownId"></param>
    // /// <returns></returns>
    // [HttpDelete("dropdowns/options/{optionId:guid}")]
    // public async Task<IActionResult> DeleteDropdownOption( Guid optionId, [FromQuery] Guid dropdownId )
    // {
    //     await _formDesignerService.DeleteDropdownOption( optionId );
    //     var options = await _formDesignerService.GetDropdownOptions( dropdownId );
    //     return Ok(options);
    // }
    
    // ────────── Form Header ──────────
    
    /// <summary>
    /// 儲存多對多表單主檔資訊並建立對應的主 / 目標 / 關聯表設定。
    /// MAPPING_TABLE必須要有 SID(DECIMAL(15,0)) 欄位
    /// </summary>
    [HttpPost("headers")]
    public async Task<IActionResult> SaveMultipleMappingFormHeader([FromBody] MultipleMappingFormHeaderViewModel model)
    {
        if (model.BASE_TABLE_ID == Guid.Empty ||
            model.DETAIL_TABLE_ID == Guid.Empty ||
            model.MAPPING_TABLE_ID == Guid.Empty)
        {
            return BadRequest("BASE_TABLE_ID / DETAIL_TABLE_ID / MAPPING_TABLE_ID 不可為空");
        }

        if (string.IsNullOrWhiteSpace(model.MAPPING_BASE_FK_COLUMN) ||
            string.IsNullOrWhiteSpace(model.MAPPING_DETAIL_FK_COLUMN))
        {
            return BadRequest("MAPPING_BASE_FK_COLUMN / MAPPING_DETAIL_FK_COLUMN 不可為空");
        }

        var id = await _formDesignerService.SaveMultipleMappingFormHeader(model);
        return Ok(new { id });
    }
}

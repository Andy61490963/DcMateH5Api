using System;
using ClassLibrary;
using DcMateH5Api.Areas.Form.Interfaces;
using DcMateH5Api.Areas.Form.Models;
using DcMateH5Api.Areas.Form.ViewModels;
using Microsoft.AspNetCore.Mvc;
using DcMateH5Api.Helper;

namespace DcMateH5Api.Areas.Form.Controllers;

[Area("Form")]
[ApiController]
[ApiExplorerSettings(GroupName = SwaggerGroups.FormWithMasterDetail)]
[Route("[area]/[controller]")]
[Produces("application/json")]
public class FormDesignerMasterDetailController : ControllerBase
{
    private readonly IFormDesignerService _formDesignerService;
    private readonly FormFunctionType _funcType = FormFunctionType.MasterDetail;
    
    public FormDesignerMasterDetailController(IFormDesignerService formDesignerService)
    {
        _formDesignerService = formDesignerService;
    }

    // ────────── Form Designer 列表 ──────────
    /// <summary>
    /// 取得表單主檔 (FORM_FIELD_Master) 清單
    /// </summary>
    /// <param name="q">關鍵字 (模糊搜尋 FORM_NAME)</param>
    /// <param name="ct">CancellationToken</param>
    /// <returns>表單主檔清單</returns>
    [HttpGet]
    public async Task<ActionResult<List<FORM_FIELD_Master>>> GetFormMasters(
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
        await _formDesignerService.UpdateFormName(model.ID, model.FORM_NAME, ct);
        return Ok();
    }
    
    /// <summary>
    /// 刪除指定的主檔 or 明細 or 檢視表資料
    /// </summary>
    /// <param name="id">FORM_FIELD_Master 的唯一識別編號</param>
    /// <returns>NoContent 回應</returns>
    [HttpDelete("{id}")]
    public IActionResult Delete(Guid id)
    {
        _formDesignerService.DeleteFormMaster(id);
        return NoContent();
    }
    
    // ────────── Form Designer 入口 ──────────
    /// <summary>
    /// 取得指定的 主檔 and 明細 and 檢視表 主畫面資料(請傳入父節點 masterId)
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
    /// 依表名稱關鍵字搜尋 表 或 檢視表，並回傳列表。
    /// </summary>
    [HttpGet("tables/tableName")]
    public IActionResult SearchTables(string? tableName, [FromQuery] TableSchemaQueryType schemaType)
    {
        try
        {
            var result = _formDesignerService.SearchTables(tableName, schemaType);
            if (result.Count == 0) return NotFound();
            return Ok(result);
        }
        catch (HttpStatusCodeException ex)
        {
            return StatusCode((int)ex.StatusCode, ex.Message);
        }
    }
    
    /// <summary>
    /// 取得資料表所有欄位設定(tableName必須傳，如果傳入空formMasterId，會創建一筆新的，如果有傳入formMasterId，會取得舊的)
    /// </summary>
    [HttpGet("tables/{tableName}/fields")]
    public IActionResult GetFields(string tableName, Guid? formMasterId, [FromQuery] TableSchemaQueryType schemaType)
    {
        try
        {
            var result = _formDesignerService.EnsureFieldsSaved(tableName, formMasterId, schemaType);

            if (result == null)
            {
                return NotFound();
            }
            return Ok(result);
        }
        catch (HttpStatusCodeException ex)
        {
            return StatusCode((int)ex.StatusCode, ex.Message);
        }
    }
    
    /// <summary>
    /// 依欄位設定 ID 取得單一欄位設定
    /// </summary>
    /// <param name="fieldId">欄位設定唯一識別碼</param>
    [HttpGet("fields/{fieldId}")]
    public IActionResult GetField(Guid fieldId)
    {
        var field = _formDesignerService.GetFieldById(fieldId);
        if (field == null) return NotFound();
        return Ok(field);
    }
    
    /// <summary>
    /// 新增或更新單一欄位設定（ID 有值為更新，無值為新增）
    /// </summary>
    // [RequirePermission(ActionAuthorizeHelper.View)]
    [HttpPost("fields")]
    public IActionResult UpsertField([FromBody] FormFieldViewModel model)
    {
        try
        {
            if (model.SchemaType == TableSchemaQueryType.OnlyTable &&
                (model.QUERY_COMPONENT != QueryComponentType.None ||
                 model.CAN_QUERY == true))
                return Conflict("無法往主表寫入查詢條件");
            
            if (model.SchemaType == TableSchemaQueryType.OnlyTable &&
                (model.QUERY_DEFAULT_VALUE != null ||
                 model.CAN_QUERY == true))
                return Conflict("無法往主表寫入查詢預設值");
            
            if (model.SchemaType == TableSchemaQueryType.OnlyView &&
                (model.CAN_QUERY == false && model.QUERY_COMPONENT != QueryComponentType.None))
                return Conflict("無法更改未開放查詢條件的查詢元件");
            
            if (model.ID != Guid.Empty &&
                _formDesignerService.HasValidationRules(model.ID) &&
                _formDesignerService.GetControlTypeByFieldId(model.ID) != model.CONTROL_TYPE)
                return Conflict("已有驗證規則，無法變更控制元件類型");

            var master = new FORM_FIELD_Master { ID = model.FORM_FIELD_Master_ID };
            var formMasterId = _formDesignerService.GetOrCreateFormMasterId(master);

            _formDesignerService.UpsertField(model, formMasterId);
            var fields = _formDesignerService.GetFieldsByTableName(model.TableName, formMasterId, model.SchemaType);
            fields.ID = formMasterId;
            fields.SchemaQueryType = model.SchemaType;
            return Ok(fields);
        }
        catch (HttpStatusCodeException ex)
        {
            return StatusCode((int)ex.StatusCode, ex.Message);
        }
    }
    
    /// <summary>
    /// 批次設定所有欄位為可編輯/不可編輯
    /// </summary>
    [HttpPost("tables/{tableName}/fields/batch-editable")]
    public IActionResult BatchSetEditable(
        [FromQuery] Guid formMasterId,
        string tableName,
        [FromQuery] bool isEditable,
        [FromQuery] TableSchemaQueryType schemaType)
    {
        if (schemaType != TableSchemaQueryType.OnlyTable)
            return BadRequest("僅支援主檔欄位清單的批次設定。");

        _formDesignerService.SetAllEditable(formMasterId, tableName, isEditable);
        var fields = _formDesignerService.GetFieldsByTableName(tableName, formMasterId, schemaType);
        fields.ID = formMasterId;
        fields.SchemaQueryType = schemaType;
        return Ok(fields);
    }
    
    /// <summary>
    /// 批次設定所有欄位為必填/非必填
    /// </summary>
    [HttpPost("tables/{tableName}/fields/batch-required")]
    public IActionResult BatchSetRequired(
        [FromQuery] Guid formMasterId,
        string tableName,
        [FromQuery] bool isRequired,
        [FromQuery] TableSchemaQueryType schemaType)
    {
        if (schemaType != TableSchemaQueryType.OnlyTable)
            return BadRequest("僅支援主檔欄位清單的批次設定。");

        _formDesignerService.SetAllRequired(formMasterId, tableName, isRequired);
        var fields = _formDesignerService.GetFieldsByTableName(tableName, formMasterId, schemaType);
        fields.ID = formMasterId;
        fields.SchemaQueryType = schemaType;
        return Ok(fields);
    }
    
    // ────────── 欄位驗證規則 ──────────

    /// <summary>
    /// 取得欄位驗證規則與驗證類型選項
    /// </summary>
    [HttpGet("fields/{fieldId:guid}/rules")]
    public IActionResult GetValidationRules(Guid fieldId)
    {
        if (fieldId == Guid.Empty)
            return BadRequest("請先設定控制元件後再新增驗證條件。");

        // var options = GetValidationTypeOptions(fieldId);
        var rules = _formDesignerService.GetValidationRulesByFieldId(fieldId);
        return Ok(new { rules });   
    }
    
    /// <summary>
    /// 新增一筆空的驗證規則並回傳全部規則
    /// </summary>
    [HttpPost("fields/{fieldId:guid}/rules")]
    public IActionResult AddEmptyValidationRule(Guid fieldId)
    {
        // var options = GetValidationTypeOptions(fieldId);
        var rule = _formDesignerService.CreateEmptyValidationRule(fieldId);
        _formDesignerService.InsertValidationRule(rule);
        var rules = _formDesignerService.GetValidationRulesByFieldId(fieldId);
        return Ok(new { rules });
    }
    
    /// <summary>
    /// 更新單一驗證規則
    /// </summary>
    [HttpPut("rules/{id:guid}")]
    public IActionResult UpdateValidationRule([FromBody] FormFieldValidationRuleDto rule)
    {
        _formDesignerService.SaveValidationRule(rule);
        return Ok();
    }

    /// <summary>
    /// 刪除驗證規則
    /// </summary>
    [HttpDelete("rules/{id:guid}")]
    public IActionResult DeleteValidationRule(Guid id, [FromQuery] Guid fieldConfigId)
    {
        _formDesignerService.DeleteValidationRule(id);
        // var options = GetValidationTypeOptions(fieldConfigId);
        var rules = _formDesignerService.GetValidationRulesByFieldId(fieldConfigId);
        return Ok(new { rules });
    }
    
    // ────────── Dropdown ──────────

    /// <summary>
    /// 取得下拉選單設定（不存在則自動建立）
    /// </summary>
    [HttpGet("fields/{fieldId:guid}/dropdown")]
    public IActionResult GetDropdownSetting(Guid fieldId)
    {
        var field = _formDesignerService.GetFieldById(fieldId);
        if (field == null)
        {
            return BadRequest("查無此設定檔，請確認ID是否正確。");
        }
        if (field.SchemaType != TableSchemaQueryType.OnlyView)
        {
            return BadRequest("查詢條件僅支援View表。");
        }
        _formDesignerService.EnsureDropdownCreated(fieldId);
        var setting = _formDesignerService.GetDropdownSetting(fieldId);
        return Ok(setting);
    }

    /// <summary>
    /// 設定下拉選單資料來源模式（SQL/設定檔）
    /// </summary>
    [HttpPut("dropdowns/{dropdownId:guid}/mode")]
    public IActionResult SetDropdownMode(Guid dropdownId, [FromQuery] bool isUseSql)
    {
        _formDesignerService.SetDropdownMode(dropdownId, isUseSql);
        return Ok();
    }

    /// <summary>
    /// 儲存下拉選單 SQL 查詢
    /// </summary>
    [HttpPut("dropdowns/{dropdownId:guid}/sql")]
    public IActionResult SaveDropdownSql(Guid dropdownId, [FromBody] string sql)
    {
        _formDesignerService.SaveDropdownSql(dropdownId, sql);
        return Ok();
    }

    /// <summary>
    /// 驗證下拉 SQL 語法
    /// </summary>
    [HttpPost("dropdowns/validate-sql")]
    public IActionResult ValidateDropdownSql([FromBody] string sql)
    {
        var res = _formDesignerService.ValidateDropdownSql(sql);
        return Ok(res);
    }

    /// <summary>
    /// 匯入下拉選單選項（由 SQL 查詢）
    /// </summary>
    [HttpPost("dropdowns/{dropdownId:guid}/import-options")]
    public IActionResult ImportDropdownOptions(Guid dropdownId, [FromBody] ImportOptionDto dto)
    {
        var res = _formDesignerService.ImportDropdownOptionsFromSql(dto.Sql, dropdownId);
        if (!res.Success) return BadRequest(res.Message);

        var options = _formDesignerService.GetDropdownOptions(dropdownId);
        return Ok(options);
    }

    /// <summary>
    /// 建立一筆空白下拉選項
    /// </summary>
    [HttpPost("dropdowns/{dropdownId:guid}/options")]
    public IActionResult CreateDropdownOption(Guid dropdownId)
    {
        _formDesignerService.SaveDropdownOption(null, dropdownId, "", "");
        var options = _formDesignerService.GetDropdownOptions(dropdownId);
        return Ok(options);
    }

    /// <summary>
    /// 儲存單筆下拉選項（新增/更新）
    /// </summary>
    [HttpPut("dropdowns/{dropdownId:guid}/options")]
    public IActionResult SaveDropdownOption(Guid dropdownId, [FromBody] SaveOptionDto dto)
    {
        _formDesignerService.SaveDropdownOption(dto.Id, dropdownId, dto.OptionText, dto.OptionValue);
        return Ok();
    }

    /// <summary>
    /// 刪除下拉選項
    /// </summary>
    [HttpDelete("dropdowns/options/{optionId:guid}")]
    public IActionResult DeleteDropdownOption(Guid optionId, [FromQuery] Guid dropdownId)
    {
        _formDesignerService.DeleteDropdownOption(optionId);
        var options = _formDesignerService.GetDropdownOptions(dropdownId);
        return Ok(options);
    }
    
    /// <summary>
    /// 儲存 Master/Detail 表單主檔資訊
    /// </summary>
    [HttpPost("headers")]
    public IActionResult SaveMasterDetailFormHeader([FromBody] MasterDetailFormHeaderViewModel model)
    {
        if (model.MASTER_TABLE_ID == Guid.Empty ||
            model.DETAIL_TABLE_ID == Guid.Empty ||
            model.VIEW_TABLE_ID == Guid.Empty)
        {
            return BadRequest("MASTER_TABLE_ID / DETAIL_TABLE_ID / VIEW_TABLE_ID 不可為空");
        }

        if (_formDesignerService.CheckMasterDetailFormMasterExists(
                model.MASTER_TABLE_ID,
                model.DETAIL_TABLE_ID,
                model.VIEW_TABLE_ID,
                model.ID))
        {
            return Conflict("相同的 Master/Detail/View 組合已存在");
        }

        var id = _formDesignerService.SaveMasterDetailFormHeader(model);
        return Ok(new { id });
    }
}

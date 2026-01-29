using ClassLibrary;
using DcMateH5Api.Areas.Form.Interfaces;
using DcMateH5Api.Areas.Form.Models;
using DcMateH5Api.Areas.Form.ViewModels;
using DcMateH5Api.Helper;
using Microsoft.AspNetCore.Mvc;

namespace DcMateH5Api.Areas.Form.Controllers;

[Area("Form")]
[ApiController]
[ApiExplorerSettings(GroupName = SwaggerGroups.FormWithMasterDetail)]
[Route("[area]/[controller]")]
[Produces("application/json")]
public class FormDesignerMasterDetailController : ControllerBase
{
    private readonly IFormDesignerService _formDesignerService;
    private readonly FormFunctionType _funcType = FormFunctionType.MasterDetailMaintenance;

    private static class Routes
    {
        // Master
        public const string UpdateFormName = "form-name";
        public const string ById = "{id}";
        public const string ByIdGuid = "{id:guid}";

        // Tables / Fields
        public const string SearchTables = "tables/tableName";
        public const string TableFields = "tables/{tableName}/fields";
        public const string GetField = "fields/{fieldId}";
        public const string UpsertField = "fields";

        // Batch
        public const string BatchEditable = "tables/fields/batch-editable";
        public const string BatchRequired = "tables/fields/batch-required";

        // Validation Rules
        public const string FieldRules = "fields/{fieldId:guid}/rules";
        public const string UpdateRule = "rules";
        public const string DeleteRule = "rules/{id:guid}";

        // Dropdown
        public const string FieldDropdown = "fields/{fieldId:guid}/dropdown";
        public const string SetDropdownMode = "dropdowns/{dropdownId:guid}/mode";
        public const string GetDropdownOptionsLegacy = "dropdowns/{dropdownId:guid}";
        public const string ValidateDropdownSql = "dropdowns/validate-sql";
        public const string ReplaceDropdownOptions = "dropdowns/{dropdownId:guid}/options:replace";

        // Header
        public const string Headers = "headers";
    }

    public FormDesignerMasterDetailController(IFormDesignerService formDesignerService)
    {
        _formDesignerService = formDesignerService;
    }

    // ────────── Master ──────────
    #region Master

    /// <summary>
    /// 取得表單主檔 (FORM_FIELD_MASTER) 清單
    /// </summary>
    /// <param name="q">關鍵字 (模糊搜尋 FORM_NAME)</param>
    /// <param name="ct">CancellationToken</param>
    /// <returns>表單主檔清單</returns>
    [HttpGet]
    [ProducesResponseType(typeof(List<FormFieldMasterDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<FormFieldMasterDto>>> GetFormMasters([FromQuery] string? q, CancellationToken ct)
    {
        try
        {
            var masters = await _formDesignerService.GetFormMasters(_funcType, q, ct);
            return Ok(masters);
        }
        catch (HttpStatusCodeException ex)
        {
            return StatusCode((int)ex.StatusCode, ex.Message);
        }
    }

    /// <summary>
    /// 更新主檔 or 明細 or 檢視表 名稱
    /// </summary>
    [HttpPut(Routes.UpdateFormName)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateFormName([FromBody] UpdateFormNameViewModel model, CancellationToken ct)
    {
        try
        {
            await _formDesignerService.UpdateFormName(model, ct);
            return Ok();
        }
        catch (HttpStatusCodeException ex)
        {
            return StatusCode((int)ex.StatusCode, ex.Message);
        }
    }

    /// <summary>
    /// 刪除指定的主檔 or 明細 or 檢視表資料
    /// </summary>
    /// <param name="id">FORM_FIELD_MASTER 的唯一識別編號</param>
    /// <param name="ct">CancellationToken</param>
    /// <returns>NoContent 回應</returns>
    [HttpDelete(Routes.ById)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Delete([FromRoute] Guid id, CancellationToken ct)
    {
        try
        {
            await _formDesignerService.DeleteFormMaster(id);
            return NoContent();
        }
        catch (HttpStatusCodeException ex)
        {
            return StatusCode((int)ex.StatusCode, ex.Message);
        }
    }

    /// <summary>
    /// 取得指定的 主檔 and 明細 and 檢視表 主畫面資料(請傳入父節點 masterId)
    /// </summary>
    [HttpGet(Routes.ByIdGuid)]
    [ProducesResponseType(typeof(FormDesignerIndexViewModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDesigner([FromRoute] Guid id, CancellationToken ct)
    {
        try
        {
            var model = await _formDesignerService.GetFormDesignerIndexViewModel(_funcType, id, ct);
            return Ok(model);
        }
        catch (HttpStatusCodeException ex)
        {
            return StatusCode((int)ex.StatusCode, ex.Message);
        }
    }

    #endregion

    // ────────── Tables / Fields ──────────
    #region Tables / Fields

    /// <summary>
    /// 依名稱關鍵字查詢資料表或檢視表名稱清單(目前列出全部)
    /// 支援前綴與模糊比對（使用 LIKE）。
    /// </summary>
    /// <param name="tableName">名稱</param>
    /// <param name="queryType">欲搜尋的資料來源類型（主表或檢視表）</param>
    /// <returns>符合條件的表名稱集合</returns>
    [HttpGet(Routes.SearchTables)]
    [ProducesResponseType(typeof(List<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public IActionResult SearchTables([FromQuery] string? tableName, [FromQuery] TableQueryType queryType)
    {
        try
        {
            var result = _formDesignerService.SearchTables(tableName, queryType);
            if (result.Count == 0)
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
    /// 取得資料表所有欄位設定(tableName必須傳，如果傳入空formMasterId，會創建一筆新的，如果有傳入formMasterId，會取得舊的)
    /// </summary>
    [HttpGet(Routes.TableFields)]
    [ProducesResponseType(typeof(List<FormFieldViewModel>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetFields(
        [FromRoute] string tableName,
        [FromQuery] Guid? formMasterId,
        [FromQuery] TableSchemaQueryType schemaType,
        CancellationToken ct)
    {
        try
        {
            var result = await _formDesignerService.EnsureFieldsSaved(tableName, formMasterId, schemaType, ct);
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
    /// 依欄位設定 ID 取得單一欄位設定 ( GetFields搜尋時就會先預先建立完成 )
    /// </summary>
    [HttpGet(Routes.GetField)]
    [ProducesResponseType(typeof(FormFieldViewModel), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetField([FromRoute] Guid fieldId, CancellationToken ct)
    {
        try
        {
            var field = await _formDesignerService.GetFieldById(fieldId);
            if (field == null)
            {
                return NotFound();
            }

            return Ok(field);
        }
        catch (HttpStatusCodeException ex)
        {
            return StatusCode((int)ex.StatusCode, ex.Message);
        }
    }

    /// <summary>
    /// 新增或更新單一欄位設定（ID 有值為更新，無值為新增）
    /// </summary>
    [HttpPost(Routes.UpsertField)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpsertField([FromBody] FormFieldViewModel model, CancellationToken ct)
    {
        try
        {
            var conflict = ValidateUpsertField(model);
            if (conflict != null)
            {
                return conflict;
            }

            var master = new FormFieldMasterDto
            {
                ID = model.FORM_FIELD_MASTER_ID
            };

            var formMasterId = await _formDesignerService.GetOrCreateFormMasterIdAsync(master);

            await _formDesignerService.UpsertFieldAsync(model, formMasterId, ct);

            var fields = await _formDesignerService.GetFieldsByTableName(model.TableName, formMasterId, model.SchemaType);
            return Ok(fields);
        }
        catch (HttpStatusCodeException ex)
        {
            return StatusCode((int)ex.StatusCode, ex.Message);
        }
    }

    #endregion

    // ────────── Batch ──────────
    #region Batch

    /// <summary>
    /// 批次設定所有欄位為可編輯/不可編輯
    /// </summary>
    [HttpPost(Routes.BatchEditable)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> BatchSetEditable([FromQuery] Guid formMasterId, [FromQuery] bool isEditable, CancellationToken ct)
    {
        try
        {
            var tableName = await _formDesignerService.SetAllEditable(formMasterId, isEditable, ct);
            var fields = await _formDesignerService.GetFieldsByTableName(tableName, formMasterId, TableSchemaQueryType.OnlyTable);
            return Ok(fields);
        }
        catch (HttpStatusCodeException ex)
        {
            return StatusCode((int)ex.StatusCode, ex.Message);
        }
    }

    /// <summary>
    /// 批次設定所有欄位為必填/非必填
    /// </summary>
    [HttpPost(Routes.BatchRequired)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> BatchSetRequired([FromQuery] Guid formMasterId, [FromQuery] bool isRequired, CancellationToken ct)
    {
        try
        {
            var tableName = await _formDesignerService.SetAllRequired(formMasterId, isRequired, ct);
            var fields = await _formDesignerService.GetFieldsByTableName(tableName, formMasterId, TableSchemaQueryType.OnlyTable);
            return Ok(fields);
        }
        catch (HttpStatusCodeException ex)
        {
            return StatusCode((int)ex.StatusCode, ex.Message);
        }
    }

    #endregion

    // ────────── Validation Rules ──────────
    #region Validation Rules

    /// <summary>
    /// 新增一筆空的驗證規則並回傳全部規則
    /// </summary>
    [HttpPost(Routes.FieldRules)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AddEmptyValidationRule([FromRoute] Guid fieldId, CancellationToken ct = default)
    {
        try
        {
            var rule = _formDesignerService.CreateEmptyValidationRule(fieldId);
            await _formDesignerService.InsertValidationRule(rule, ct);

            var rules = await _formDesignerService.GetValidationRulesByFieldId(fieldId, ct);
            return Ok(new { rules });
        }
        catch (HttpStatusCodeException ex)
        {
            return StatusCode((int)ex.StatusCode, ex.Message);
        }
    }

    /// <summary>
    /// 取得欄位驗證規則
    /// </summary>
    [HttpGet(Routes.FieldRules)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetValidationRules([FromRoute] Guid fieldId, CancellationToken ct = default)
    {
        try
        {
            if (fieldId == Guid.Empty)
            {
                return BadRequest("請先設定控制元件後再新增驗證條件。");
            }

            var rules = await _formDesignerService.GetValidationRulesByFieldId(fieldId, ct);
            return Ok(new { rules });
        }
        catch (HttpStatusCodeException ex)
        {
            return StatusCode((int)ex.StatusCode, ex.Message);
        }
    }

    /// <summary>
    /// 更新單一驗證規則
    /// </summary>
    [HttpPut(Routes.UpdateRule)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateValidationRule([FromBody] FormFieldValidationRuleDto model, CancellationToken ct)
    {
        try
        {
            await _formDesignerService.SaveValidationRule(model);
            return Ok();
        }
        catch (HttpStatusCodeException ex)
        {
            return StatusCode((int)ex.StatusCode, ex.Message);
        }
    }

    /// <summary>
    /// 刪除驗證規則
    /// </summary>
    [HttpDelete(Routes.DeleteRule)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeleteValidationRule([FromRoute] Guid id, CancellationToken ct)
    {
        try
        {
            await _formDesignerService.DeleteValidationRule(id);
            return NoContent();
        }
        catch (HttpStatusCodeException ex)
        {
            return StatusCode((int)ex.StatusCode, ex.Message);
        }
    }

    #endregion

    // ────────── Dropdown ──────────
    #region Dropdown

    /// <summary>
    /// 取得下拉選單設定（不存在則自動建立）
    /// </summary>
    [HttpGet(Routes.FieldDropdown)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetDropdownSetting([FromRoute] Guid fieldId, CancellationToken ct)
    {
        try
        {
            var field = await _formDesignerService.GetFieldById(fieldId);
            if (field == null)
            {
                return BadRequest("查無此設定檔，請確認ID是否正確。");
            }

            _formDesignerService.EnsureDropdownCreated(fieldId);

            var setting = await _formDesignerService.GetDropdownSetting(fieldId);
            return Ok(setting);
        }
        catch (HttpStatusCodeException ex)
        {
            return StatusCode((int)ex.StatusCode, ex.Message);
        }
    }

    /// <summary>
    /// 設定下拉選單資料來源模式（SQL/設定檔）
    /// </summary>
    [HttpPut(Routes.SetDropdownMode)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SetDropdownMode([FromRoute] Guid dropdownId, [FromQuery] bool isUseSql, CancellationToken ct)
    {
        try
        {
            await _formDesignerService.SetDropdownMode(dropdownId, isUseSql, ct);
            return Ok();
        }
        catch (HttpStatusCodeException ex)
        {
            return StatusCode((int)ex.StatusCode, ex.Message);
        }
    }

    /// <summary>
    /// 取得所有下拉選單選項(排除Sql)
    /// </summary>
    /// <remarks>
    /// 注意：此端點原本使用 POST + "dropdowns/{dropdownId:guid}"，為避免行為變更，保留不動。
    /// </remarks>
    [HttpPost(Routes.GetDropdownOptionsLegacy)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetDropdownOption([FromRoute] Guid dropdownId, CancellationToken ct)
    {
        try
        {
            var options = await _formDesignerService.GetDropdownOptions(dropdownId, ct);
            return Ok(options);
        }
        catch (HttpStatusCodeException ex)
        {
            return StatusCode((int)ex.StatusCode, ex.Message);
        }
    }

    /// <summary>
    /// 驗證下拉 SQL 語法
    /// </summary>
    [HttpPost(Routes.ValidateDropdownSql)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public IActionResult ValidateDropdownSql([FromBody] string sql)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(sql))
            {
                return BadRequest("SQL 不可為空");
            }

            var res = _formDesignerService.ValidateDropdownSql(sql);
            return Ok(res);
        }
        catch (HttpStatusCodeException ex)
        {
            return StatusCode((int)ex.StatusCode, ex.Message);
        }
    }

    /// <summary>
    /// 以「前端送來的完整清單」覆蓋下拉選項（Replace All）
    /// </summary>
    [HttpPut(Routes.ReplaceDropdownOptions)]
    [ProducesResponseType(typeof(List<FormFieldDropdownOptionsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ReplaceDropdownOptions(
        [FromRoute] Guid dropdownId,
        [FromBody] List<DropdownOptionItemViewModel> options,
        CancellationToken ct)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            await _formDesignerService.ReplaceDropdownOptionsAsync(dropdownId, options, ct);

            var latestOptions = await _formDesignerService.GetDropdownOptions(dropdownId, ct);
            return Ok(latestOptions);
        }
        catch (HttpStatusCodeException ex)
        {
            return StatusCode((int)ex.StatusCode, ex.Message);
        }
    }

    #endregion

    // ────────── Header ──────────
    #region Header

    /// <summary>
    /// 儲存 Master/Detail 表單主檔資訊
    /// </summary>
    [HttpPost(Routes.Headers)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SaveMasterDetailFormHeader([FromBody] MasterDetailFormHeaderViewModel model, CancellationToken ct)
    {
        try
        {
            if (model.BASE_TABLE_ID == Guid.Empty ||
                model.DETAIL_TABLE_ID == Guid.Empty ||
                model.VIEW_TABLE_ID == Guid.Empty)
            {
                return BadRequest("MASTER_TABLE_ID / DETAIL_TABLE_ID / VIEW_TABLE_ID 不可為空");
            }

            var id = await _formDesignerService.SaveMasterDetailFormHeader(model, ct);
            return Ok(new { id });
        }
        catch (HttpStatusCodeException ex)
        {
            return StatusCode((int)ex.StatusCode, ex.Message);
        }
    }

    #endregion

    // ────────── Private Helpers ──────────
    #region Private Helpers

    private IActionResult? ValidateUpsertField(FormFieldViewModel model)
    {
        if (model.SchemaType == TableSchemaQueryType.OnlyTable &&
            (model.QUERY_COMPONENT != QueryComponentType.None || model.CAN_QUERY == true))
        {
            return Conflict("無法往主表寫入查詢條件");
        }

        if (model.SchemaType == TableSchemaQueryType.OnlyTable &&
            (model.QUERY_DEFAULT_VALUE != null || model.CAN_QUERY == true))
        {
            return Conflict("無法往主表寫入查詢預設值");
        }

        if (model.SchemaType == TableSchemaQueryType.OnlyView &&
            (model.CAN_QUERY == false && model.QUERY_COMPONENT != QueryComponentType.None))
        {
            return Conflict("無法更改未開放查詢條件的查詢元件");
        }

        if (model.ID != Guid.Empty &&
            _formDesignerService.HasValidationRules(model.ID) &&
            _formDesignerService.GetControlTypeByFieldId(model.ID) != model.CONTROL_TYPE)
        {
            return Conflict("已有驗證規則，無法變更控制元件類型");
        }

        return null;
    }

    #endregion
}

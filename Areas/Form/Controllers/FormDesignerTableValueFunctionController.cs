using ClassLibrary;
using DcMateH5Api.Areas.Form.Interfaces;
using DcMateH5Api.Areas.Form.Models;
using DcMateH5Api.Areas.Form.ViewModels;
using DcMateH5Api.Controllers;
using DcMateH5Api.Helper;
using Microsoft.AspNetCore.Mvc;

namespace DcMateH5Api.Areas.Form.Controllers;

[Area("Form")]
[ApiController]
[ApiExplorerSettings(GroupName = SwaggerGroups.FormTableValueFunction)]
[Route("[area]/[controller]")]
public class FormDesignerTableValueFunctionController : BaseController
{
    private readonly IFormDesignerService _formDesignerService;
    private readonly IFormDesignerTableValueFunctionService _formDesignerTableValueFunctionService;
    private readonly FormFunctionType _funcType = FormFunctionType.TableValueFunctionMaintenance;

    public FormDesignerTableValueFunctionController(IFormDesignerService formDesignerService, IFormDesignerTableValueFunctionService formDesignerTableValueFunctionService)
    {
        _formDesignerService = formDesignerService;
        _formDesignerTableValueFunctionService = formDesignerTableValueFunctionService;
    }
    
    private static class Routes
    {
        // Master
        public const string UpdateFormName = "form-name";
        public const string ById = "{id}";
        public const string ByIdGuid = "{id:guid}";

        // Tables / Fields
        public const string SearchTables = "tables/tableName";
        public const string TableFields = "tables/{tvfName}/fields";
        public const string GetField = "fields/{fieldId}";
        public const string UpsertField = "fields";
        public const string MoveField = "fields/move";
        public const string BatchEditable = "tables/fields/batch-editable";
        public const string BatchRequired = "tables/fields/batch-required";

        // Validation Rules
        public const string FieldRules = "fields/{fieldId:guid}/rules";
        public const string UpdateRule = "rules";
        public const string DeleteRule = "rules/{id:guid}";

        // Dropdown
        public const string GetDropdown = "dropdowns/{dropdownId:guid}";
        public const string SetDropdownMode = "dropdowns/{dropdownId:guid}/mode";
        public const string GetDropdownOptions = "dropdowns/{dropdownId:guid}/options";
        public const string ValidateDropdownSql = "dropdowns/validate-sql";
        public const string ImportPreviousQueryValues = "dropdowns/{dropdownId:guid}/import-previous-query-values";
        public const string ReplaceDropdownOptions = "dropdowns/{dropdownId:guid}/options:replace";

        // Delete Guard SQL
        public const string DeleteGuardSqls = "delete-guard-sqls";
        public const string DeleteGuardSqlById = "delete-guard-sqls/{id:guid}";

        // Header
        public const string SaveHeaders = "headers";
    }
    
    /// <summary>
    /// 取得指定的 Table Value Function 主畫面資料(請傳入父節點 masterId)
    /// </summary>
    /// <param name="id">FORM_FIELD_MASTER 的ID</param>
    /// <param name="ct">CancellationToken</param>
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
    
    /// <summary>
    /// 依名稱關鍵字查詢 Table Value Function
    /// 支援前綴與模糊比對（使用 LIKE）。
    /// </summary>
    /// <param name="tvpName">名稱</param>
    /// <returns>符合條件的表名稱集合</returns>
    [HttpGet(Routes.SearchTables)]
    [ProducesResponseType(typeof(List<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public IActionResult SearchTables([FromQuery] string? tvpName)
    {
        try
        {
            var result = _formDesignerService.SearchTables(tvpName, TableQueryType.OnlyFunction);
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
    /// 取得 Table Value Function 資料表所有欄位設定(tableName必須傳，如果傳入空formMasterId，會創建一筆新的，如果有傳入formMasterId，會取得舊的)
    /// </summary>
    [HttpGet(Routes.TableFields)]
    [ProducesResponseType(typeof(List<FormFieldViewModel>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetFields(
        [FromRoute] string tvfName,
        [FromQuery] Guid? formMasterId,
        CancellationToken ct)
    {
        try
        {
            var result = await _formDesignerTableValueFunctionService.EnsureFieldsSaved(tvfName, formMasterId, TableSchemaQueryType.OnlyTvf, ct);
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
    [ProducesResponseType(typeof(List<FormFieldViewModel>), StatusCodes.Status200OK)]
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

            await _formDesignerService.UpsertFieldAsync(model, model.FORM_FIELD_MASTER_ID, ct);
            
            var fields = await _formDesignerTableValueFunctionService
                .GetFieldsByTableNameInTxAsync(model.TableName, model.FORM_FIELD_MASTER_ID, model.SchemaType, ct)
                .ConfigureAwait(false);

            return Ok(fields);
        }
        catch (HttpStatusCodeException ex)
        {
            return StatusCode((int)ex.StatusCode, ex.Message);
        }
    }
    
    #region Dropdown

    /// <summary>
    /// 取得下拉選單設定（不存在則自動建立）
    /// </summary>
    [HttpGet(Routes.GetDropdown)]
    [ProducesResponseType(typeof(DropDownViewModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetDropdownSetting([FromRoute] Guid dropdownId, CancellationToken ct)
    {
        try
        {
            var setting = await _formDesignerService.GetDropdownSetting(dropdownId);
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
    /// 取得所有下拉選單選項
    /// </summary>
    [HttpPost(Routes.GetDropdownOptions)]
    [ProducesResponseType(typeof(List<FormFieldDropdownOptionsDto>), StatusCodes.Status200OK)]
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
    [ProducesResponseType(typeof(ValidateSqlResultViewModel), StatusCodes.Status200OK)]
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
    /// 匯入先前查詢的下拉選單值（僅允許 SELECT，結果需使用 AS NAME）。
    /// </summary>
    [HttpPost(Routes.ImportPreviousQueryValues)]
    [ProducesResponseType(typeof(PreviousQueryDropdownImportResultViewModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public IActionResult ImportPreviousQueryDropdownValues(
        [FromRoute] Guid dropdownId,
        [FromQuery] bool isQueryDropdwon,
        [FromBody] ImportOptionViewModel dto)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(dto.Sql))
            {
                return BadRequest("SQL 不可為空");
            }

            var res = _formDesignerService.ImportPreviousQueryDropdownValues(dto.Sql, dropdownId, isQueryDropdwon);
            if (!res.Success)
            {
                return BadRequest(res.Message);
            }

            return Ok(res);
        }
        catch (HttpStatusCodeException ex)
        {
            return StatusCode((int)ex.StatusCode, ex.Message);
        }
    }

    /// <summary>
    /// 使用者自訂的下拉選項，以「前端送來的完整清單」覆蓋下拉選項（Replace All）
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
    
    /// <summary>
    /// 儲存 Table Value Function 設定檔
    /// </summary>
    [HttpPost(Routes.SaveHeaders)]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SaveFormHeader([FromBody] FormHeaderTableValueFunctionViewModel model, CancellationToken ct)
    {
        try
        {
            if (model.TVF_TABLE_ID == Guid.Empty )
            {
                return BadRequest("TVP_TABLE_ID 不可為空");
            }

            var id = await _formDesignerTableValueFunctionService.SaveTableValueFunctionFormHeader(model, ct);
            return Ok(new { id });
        }
        catch (HttpStatusCodeException ex)
        {
            return StatusCode((int)ex.StatusCode, ex.Message);
        }
    }
    
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
        
        if (model.SchemaType == TableSchemaQueryType.OnlyTable &&
            (model.QUERY_COMPONENT != QueryComponentType.None || model.CAN_QUERY == true))
        {
            return Conflict("無法往主表寫入查詢條件");
        }

        return null;
    }
    
    #endregion
}
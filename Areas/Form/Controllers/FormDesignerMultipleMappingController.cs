using ClassLibrary;
using DcMateH5Api.Areas.Form.Interfaces;
using DcMateH5Api.Areas.Form.Models;
using DcMateH5Api.Areas.Form.ViewModels;
using DcMateH5Api.Helper;
using Microsoft.AspNetCore.Mvc;

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

    private static class Routes
    {
        // Master
        public const string UpdateFormName = "form-name";
        public const string ById = "{id:guid}";

        // Tables / Fields
        public const string SearchTables = "tables/tableName";
        public const string TableFields = "tables/{tableName}/fields";
        public const string UpsertField = "fields";
        public const string MoveField = "fields/move";

        // Dropdown
        public const string GetDropdown = "dropdowns/{dropdownId:guid}";
        public const string SetDropdownMode = "dropdowns/{dropdownId:guid}/mode";
        public const string GetDropdownOptions = "dropdowns/{dropdownId:guid}/options";
        public const string ValidateDropdownSql = "dropdowns/validate-sql";
        public const string ImportPreviousQueryValues = "dropdowns/{dropdownId:guid}/import-previous-query-values";
        public const string ReplaceDropdownOptions = "dropdowns/{dropdownId:guid}/options:replace";

        // Header
        public const string Headers = "headers";
    }

    public FormDesignerMultipleMappingController(IFormDesignerService formDesignerService)
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
    /// <param name="model">更新資料</param>
    /// <param name="ct">CancellationToken</param>
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
    /// 取得指定的主檔、目標表與關聯表（含檢視表）主畫面資料(請傳入父節點 masterId)
    /// </summary>
    [HttpGet(Routes.ById)]
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

    /// <summary>
    /// 移動表單欄位的顯示順序（使用 分數索引排序 演算法）。
    /// </summary>
    [HttpPost(Routes.MoveField)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> MoveField([FromBody] MoveFormFieldRequest req, CancellationToken ct)
    {
        try
        {
            await _formDesignerService.MoveFieldAsync(req, ct);
            return Ok();
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

    // ────────── Header ──────────
    #region Header

    /// <summary>
    /// 儲存多對多表單主檔資訊並建立對應的主 / 目標 / 關聯表設定。
    /// MAPPING_TABLE必須要有 SID(DECIMAL(15,0)) 欄位
    /// </summary>
    [HttpPost(Routes.Headers)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SaveMultipleMappingFormHeader([FromBody] MultipleMappingFormHeaderViewModel model, CancellationToken ct)
    {
        try
        {
            var id = await _formDesignerService.SaveMultipleMappingFormHeader(model, ct);
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

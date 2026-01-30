using ClassLibrary;
using DcMateH5Api.Areas.Form.Interfaces;
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
        public const string TableFields = "tables/{tableName}/fields";
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
    /// 取得 TVF 資料表所有欄位設定(tableName必須傳，如果傳入空formMasterId，會創建一筆新的，如果有傳入formMasterId，會取得舊的)
    /// </summary>
    [HttpGet(Routes.TableFields)]
    [ProducesResponseType(typeof(List<FormFieldViewModel>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetFields(
        [FromRoute] string tableName,
        [FromQuery] Guid? formMasterId,
        CancellationToken ct)
    {
        try
        {
            var result = await _formDesignerTableValueFunctionService.EnsureFieldsSaved(tableName, formMasterId, TableSchemaQueryType.OnlyTvf, ct);
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
    /// 儲存表單主檔資訊
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
}
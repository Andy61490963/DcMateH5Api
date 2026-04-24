using DcMateClassLibrary.Enums.Form;
using DcMateClassLibrary.Helper;
using DcMateClassLibrary.Helper.HttpHelper;
using DcMateH5.Abstractions.Form.Form;
using DcMateH5.Abstractions.Form.Models;
using DcMateH5.Abstractions.Form.ViewModels;
using DcMateH5Api.Controllers;
using Microsoft.AspNetCore.Mvc;

namespace DcMateH5Api.Areas.Form.Controllers;

[Area("Form")]
[ApiController]
[ApiExplorerSettings(GroupName = SwaggerGroups.FormView)]
[Route("[area]/[controller]")]
public class FormViewDesignerController : BaseController
{
    private readonly IFormDesignerService _formDesignerService;
    private readonly IFormViewDesignerService _formViewDesignerService;

    public FormViewDesignerController(
        IFormViewDesignerService formViewDesignerService,
        IFormDesignerService formDesignerService)
    {
        _formViewDesignerService = formViewDesignerService;
        _formDesignerService = formDesignerService;
    }

    private static class Routes
    {
        public const string UpdateFormName = "form-name";
        public const string ById = "{id:guid}";
        public const string SearchTables = "tables/tableName";
        public const string TableFields = "tables/{viewName}/fields";
        public const string GetField = "fields/{fieldId:guid}";
        public const string UpsertField = "fields";
        public const string MoveField = "fields/move";
        public const string GetDropdown = "dropdowns/{dropdownId:guid}";
        public const string SetDropdownMode = "dropdowns/{dropdownId:guid}/mode";
        public const string GetDropdownOptions = "dropdowns/{dropdownId:guid}/options";
        public const string ValidateDropdownSql = "dropdowns/validate-sql";
        public const string ImportPreviousQueryValues = "dropdowns/{dropdownId:guid}/import-previous-query-values";
        public const string ReplaceDropdownOptions = "dropdowns/{dropdownId:guid}/options:replace";
        public const string Headers = "headers";
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<FormFieldMasterDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<FormFieldMasterDto>>> GetFormMasters([FromQuery] string? q, CancellationToken ct)
    {
        try
        {
            return Ok(await _formViewDesignerService.GetFormMasters(q, ct));
        }
        catch (HttpStatusCodeException ex)
        {
            return StatusCode((int)ex.StatusCode, ex.Message);
        }
    }

    [HttpGet(Routes.ById)]
    [ProducesResponseType(typeof(FormDesignerIndexViewModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDesigner([FromRoute] Guid id, CancellationToken ct)
    {
        try
        {
            return Ok(await _formViewDesignerService.GetDesigner(id, ct));
        }
        catch (HttpStatusCodeException ex)
        {
            return StatusCode((int)ex.StatusCode, ex.Message);
        }
    }

    [HttpDelete(Routes.ById)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete([FromRoute] Guid id, CancellationToken ct)
    {
        try
        {
            await _formViewDesignerService.Delete(id, ct);
            return NoContent();
        }
        catch (HttpStatusCodeException ex)
        {
            return StatusCode((int)ex.StatusCode, ex.Message);
        }
    }

    [HttpPut(Routes.UpdateFormName)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateFormName([FromBody] UpdateFormNameViewModel model, CancellationToken ct)
    {
        try
        {
            await _formViewDesignerService.UpdateFormName(model, ct);
            return Ok();
        }
        catch (HttpStatusCodeException ex)
        {
            return StatusCode((int)ex.StatusCode, ex.Message);
        }
    }

    [HttpGet(Routes.SearchTables)]
    [ProducesResponseType(typeof(List<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult SearchTables([FromQuery] string? tableName)
    {
        try
        {
            var result = _formViewDesignerService.SearchViews(tableName);
            return result.Count == 0 ? NotFound() : Ok(result);
        }
        catch (HttpStatusCodeException ex)
        {
            return StatusCode((int)ex.StatusCode, ex.Message);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet(Routes.TableFields)]
    [ProducesResponseType(typeof(FormFieldListViewModel), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetFields([FromRoute] string viewName, [FromQuery] Guid? formMasterId, CancellationToken ct)
    {
        try
        {
            var result = await _formViewDesignerService.EnsureFieldsSaved(viewName, formMasterId, ct);
            return result == null ? NotFound() : Ok(result);
        }
        catch (HttpStatusCodeException ex)
        {
            return StatusCode((int)ex.StatusCode, ex.Message);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet(Routes.GetField)]
    [ProducesResponseType(typeof(FormFieldViewModel), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetField([FromRoute] Guid fieldId, CancellationToken ct)
    {
        try
        {
            var field = await _formViewDesignerService.GetFieldById(fieldId);
            return field == null ? NotFound() : Ok(field);
        }
        catch (HttpStatusCodeException ex)
        {
            return StatusCode((int)ex.StatusCode, ex.Message);
        }
    }

    [HttpPost(Routes.UpsertField)]
    [ProducesResponseType(typeof(FormFieldListViewModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpsertField([FromBody] FormFieldViewModel model, CancellationToken ct)
    {
        try
        {
            var conflict = ValidateUpsertField(model);
            if (conflict != null)
            {
                return conflict;
            }

            model.SchemaType = TableSchemaQueryType.OnlyView;
            await _formViewDesignerService.UpsertFieldAsync(model, ct);
            var fields = await _formViewDesignerService.GetFieldsByViewName(model.TableName, model.FORM_FIELD_MASTER_ID, ct);
            return Ok(fields);
        }
        catch (HttpStatusCodeException ex)
        {
            return StatusCode((int)ex.StatusCode, ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost(Routes.MoveField)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> MoveField([FromBody] MoveFormFieldRequest req, CancellationToken ct)
    {
        try
        {
            await _formViewDesignerService.MoveFieldAsync(req, ct);
            return Ok();
        }
        catch (HttpStatusCodeException ex)
        {
            return StatusCode((int)ex.StatusCode, ex.Message);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    // [HttpGet(Routes.GetDropdown)]
    // [ProducesResponseType(typeof(DropDownViewModel), StatusCodes.Status200OK)]
    // public async Task<IActionResult> GetDropdownSetting([FromRoute] Guid dropdownId, CancellationToken ct)
    // {
    //     try
    //     {
    //         return Ok(await _formDesignerService.GetDropdownSetting(dropdownId, ct));
    //     }
    //     catch (HttpStatusCodeException ex)
    //     {
    //         return StatusCode((int)ex.StatusCode, ex.Message);
    //     }
    // }
    //
    // [HttpPut(Routes.SetDropdownMode)]
    // [ProducesResponseType(StatusCodes.Status200OK)]
    // public async Task<IActionResult> SetDropdownMode([FromRoute] Guid dropdownId, [FromQuery] bool isUseSql, CancellationToken ct)
    // {
    //     try
    //     {
    //         await _formDesignerService.SetDropdownMode(dropdownId, isUseSql, ct);
    //         return Ok();
    //     }
    //     catch (HttpStatusCodeException ex)
    //     {
    //         return StatusCode((int)ex.StatusCode, ex.Message);
    //     }
    // }
    //
    // [HttpPost(Routes.GetDropdownOptions)]
    // [ProducesResponseType(typeof(List<FormFieldDropdownOptionsDto>), StatusCodes.Status200OK)]
    // public async Task<IActionResult> GetDropdownOptions([FromRoute] Guid dropdownId, CancellationToken ct)
    // {
    //     try
    //     {
    //         return Ok(await _formDesignerService.GetDropdownOptions(dropdownId, ct));
    //     }
    //     catch (HttpStatusCodeException ex)
    //     {
    //         return StatusCode((int)ex.StatusCode, ex.Message);
    //     }
    // }
    //
    // [HttpPost(Routes.ValidateDropdownSql)]
    // [ProducesResponseType(typeof(ValidateSqlResultViewModel), StatusCodes.Status200OK)]
    // public IActionResult ValidateDropdownSql([FromBody] string sql)
    // {
    //     try
    //     {
    //         if (string.IsNullOrWhiteSpace(sql))
    //         {
    //             return BadRequest("SQL 不可為空");
    //         }
    //
    //         return Ok(_formDesignerService.ValidateDropdownSql(sql));
    //     }
    //     catch (HttpStatusCodeException ex)
    //     {
    //         return StatusCode((int)ex.StatusCode, ex.Message);
    //     }
    // }
    //
    // [HttpPost(Routes.ImportPreviousQueryValues)]
    // [ProducesResponseType(typeof(PreviousQueryDropdownImportResultViewModel), StatusCodes.Status200OK)]
    // public IActionResult ImportPreviousQueryDropdownValues(
    //     [FromRoute] Guid dropdownId,
    //     [FromQuery] bool isQueryDropdwon,
    //     [FromBody] ImportOptionViewModel dto)
    // {
    //     try
    //     {
    //         if (string.IsNullOrWhiteSpace(dto.Sql))
    //         {
    //             return BadRequest("SQL 不可為空");
    //         }
    //
    //         var result = _formDesignerService.ImportPreviousQueryDropdownValues(dto.Sql, dropdownId, isQueryDropdwon);
    //         return result.Success ? Ok(result) : BadRequest(result.Message);
    //     }
    //     catch (HttpStatusCodeException ex)
    //     {
    //         return StatusCode((int)ex.StatusCode, ex.Message);
    //     }
    // }
    //
    // [HttpPut(Routes.ReplaceDropdownOptions)]
    // [ProducesResponseType(typeof(List<FormFieldDropdownOptionsDto>), StatusCodes.Status200OK)]
    // public async Task<IActionResult> ReplaceDropdownOptions(
    //     [FromRoute] Guid dropdownId,
    //     [FromBody] List<DropdownOptionItemViewModel> options,
    //     CancellationToken ct)
    // {
    //     try
    //     {
    //         if (!ModelState.IsValid)
    //         {
    //             return ValidationProblem(ModelState);
    //         }
    //
    //         await _formDesignerService.ReplaceDropdownOptionsAsync(dropdownId, options, ct);
    //         return Ok(await _formDesignerService.GetDropdownOptions(dropdownId, ct));
    //     }
    //     catch (HttpStatusCodeException ex)
    //     {
    //         return StatusCode((int)ex.StatusCode, ex.Message);
    //     }
    // }

    [HttpPost(Routes.Headers)]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status200OK)]
    public async Task<IActionResult> SaveFormHeader([FromBody] FormViewHeaderViewModel model, CancellationToken ct)
    {
        try
        {
            if (model.VIEW_TABLE_ID == Guid.Empty)
            {
                return BadRequest("VIEW_TABLE_ID 不可為空");
            }

            var id = await _formViewDesignerService.SaveViewFormHeader(model, ct);
            return Ok(new { id });
        }
        catch (HttpStatusCodeException ex)
        {
            return StatusCode((int)ex.StatusCode, ex.Message);
        }
    }

    private IActionResult? ValidateUpsertField(FormFieldViewModel model)
    {
        if (model.CAN_QUERY == false && model.QUERY_COMPONENT != QueryComponentType.None)
        {
            return Conflict("欄位未啟用查詢功能時，不能設定查詢元件。");
        }

        if (model.ID != Guid.Empty &&
            _formDesignerService.HasValidationRules(model.ID) &&
            _formDesignerService.GetControlTypeByFieldId(model.ID) != model.CONTROL_TYPE)
        {
            return Conflict("欄位已有驗證規則時，不能直接修改控制項類型。");
        }

        return null;
    }
}

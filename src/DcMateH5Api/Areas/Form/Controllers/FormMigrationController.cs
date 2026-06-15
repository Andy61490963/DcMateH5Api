using DcMateClassLibrary.Helper;
using DcMateClassLibrary.Helper.HttpHelper;
using DcMateH5.Abstractions.Form.Form;
using Microsoft.AspNetCore.Mvc;

namespace DcMateH5Api.Areas.Form.Controllers;

/// <summary>
/// Form settings migration APIs.
/// </summary>
[Area("Form")]
[ApiController]
[ApiExplorerSettings(GroupName = SwaggerGroups.FormMigration)]
[Route("[area]/[controller]")]
public class FormMigrationController : ControllerBase
{
    private readonly IFormDesignerService _formDesignerService;

    public FormMigrationController(IFormDesignerService formDesignerService)
    {
        _formDesignerService = formDesignerService;
    }

    /// <summary>
    /// Generate idempotent migration SQL for a form master and its related settings.
    /// </summary>
    /// <param name="id">FORM_FIELD_MASTER ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Migration SQL script.</returns>
    [HttpGet("{id:guid}/sql")]
    [Produces("text/plain")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GenerateSql([FromRoute] Guid id, CancellationToken ct)
    {
        try
        {
            var sql = await _formDesignerService.GenerateMigrationSql(id, ct);
            return Content(sql, "text/plain; charset=utf-8");
        }
        catch (HttpStatusCodeException ex)
        {
            return StatusCode((int)ex.StatusCode, ex.Message);
        }
    }
}

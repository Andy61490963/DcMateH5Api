using DcMateClassLibrary.Helper;
using DcMateClassLibrary.Helper.HttpHelper;
using DcMateH5.Abstractions.Form.Form;
using DcMateH5.Abstractions.Form.ViewModels;
using DcMateH5Api.Controllers;
using Microsoft.AspNetCore.Mvc;

namespace DcMateH5Api.Areas.Form.Controllers;

[Area("Form")]
[ApiController]
[ApiExplorerSettings(GroupName = SwaggerGroups.FormView)]
[Route("[area]/[controller]")]
public class FormViewController : BaseController
{
    private readonly IFormViewService _formViewService;

    public FormViewController(IFormViewService formViewService)
    {
        _formViewService = formViewService;
    }

    private static class Routes
    {
        public const string Masters = "masters";
        public const string Search = "search";
        public const string GetForm = "{formId:guid}";
    }

    [HttpGet(Routes.Masters)]
    [ProducesResponseType(typeof(IEnumerable<ViewFormConfigViewModel>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ViewFormConfigViewModel>>> GetFormMasters(CancellationToken ct)
    {
        try
        {
            return Ok(await _formViewService.GetFormMasters(ct));
        }
        catch (HttpStatusCodeException ex)
        {
            return StatusCode((int)ex.StatusCode, ex.Message);
        }
    }

    [HttpPost(Routes.Search)]
    [ProducesResponseType(typeof(List<FormListResponseViewModel>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetForms([FromBody] FormSearchRequest? request, CancellationToken ct)
    {
        try
        {
            if (request == null)
            {
                return BadRequest(new
                {
                    Error = "Request body is null",
                    Hint = "請提供有效的 JSON request body。"
                });
            }

            return Ok(await _formViewService.GetForms(request, ct));
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

    // [HttpPost(Routes.GetForm)]
    // [ProducesResponseType(typeof(FormSubmissionViewModel), StatusCodes.Status200OK)]
    // public async Task<IActionResult> GetForm([FromRoute] Guid formId, [FromQuery] string? pk, CancellationToken ct)
    // {
    //     try
    //     {
    //         if (formId == Guid.Empty)
    //         {
    //             return BadRequest(new { Detail = "formId 不可為空" });
    //         }
    //
    //         return Ok(await _formViewService.GetForm(formId, pk, ct));
    //     }
    //     catch (HttpStatusCodeException ex)
    //     {
    //         return StatusCode((int)ex.StatusCode, ex.Message);
    //     }
    //     catch (InvalidOperationException ex)
    //     {
    //         return BadRequest(ex.Message);
    //     }
    // }
}

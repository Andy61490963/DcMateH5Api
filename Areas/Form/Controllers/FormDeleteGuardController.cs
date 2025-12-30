using DcMateH5Api.Areas.Form.Interfaces;
using DcMateH5Api.Areas.Form.ViewModels;
using DcMateH5Api.Helper;
using Microsoft.AspNetCore.Mvc;

namespace DcMateH5Api.Areas.Form.Controllers;

/// <summary>
/// 刪除守門規則驗證 API。
/// </summary>
[Area("Form")]
[ApiController]
[ApiExplorerSettings(GroupName = SwaggerGroups.Form)]
[Route("form/delete-guard")]
public class FormDeleteGuardController : ControllerBase
{
    private readonly IFormDeleteGuardService _service;

    public FormDeleteGuardController(IFormDeleteGuardService service)
    {
        _service = service;
    }

    /// <summary>
    /// 驗證刪除守門規則，若任一規則不允許刪除則立即回傳阻擋資訊。
    /// </summary>
    /// <param name="request">刪除守門驗證請求</param>
    /// <param name="ct">取消權杖</param>
    /// <returns>刪除驗證結果</returns>
    [HttpPost("validate")]
    [ProducesResponseType(typeof(DeleteGuardValidateDataViewModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ValidateDeleteGuard(
        [FromBody] DeleteGuardValidateRequestViewModel? request,
        CancellationToken ct)
    {
        if (request == null)
        {
            return BadRequest("請提供刪除驗證請求內容。");
        }

        if (request.FormFieldMasterId == Guid.Empty)
        {
            return BadRequest("FormFieldMasterId 不可為空。");
        }

        var result = await _service.ValidateDeleteGuardAsync(request, ct);
        if (!result.IsValid)
        {
            return BadRequest(result.ErrorMessage ?? "Guard SQL 驗證失敗。");
        }

        var response = new DeleteGuardValidateDataViewModel
        {
            CanDelete = result.CanDelete,
            BlockedByRule = result.BlockedByRule
        };

        return Ok(response);
    }
}

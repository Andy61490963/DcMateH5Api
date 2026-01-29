using System.Net.Mime;
using ClassLibrary;
using DcMateH5Api.Areas.Form.Interfaces;
using DcMateH5Api.Areas.Form.Models;
using DcMateH5Api.Areas.Form.ViewModels;
using DcMateH5Api.Controllers;
using DcMateH5Api.Helper;
using Microsoft.AspNetCore.Mvc;

namespace DcMateH5Api.Areas.Form.Controllers;

/// <summary>
/// 表單主檔變更 API
/// </summary>
[Area("Form")]
[ApiController]
[ApiExplorerSettings(GroupName = SwaggerGroups.Form)]
[Route("[area]/[controller]")]
public class FormController : BaseController
{
    private readonly IFormService _formService;
    private readonly FormFunctionType _funcType = FormFunctionType.MasterMaintenance;

    private static class Routes
    {
        public const string Search = "search";
        public const string GetForm = "{formId:guid}";
        public const string DeleteWithGuard = "delete";
        public const string Export = "export";
    }

    public FormController(IFormService formService)
    {
        _formService = formService;
    }

    /// <summary>
    /// 取得主檔維護的資料列表
    /// </summary>
    /// <remarks>
    /// ### 範例輸入
    /// ```json
    /// [
    ///   {
    ///     "column": "STATUS_CALCD_TIME",
    ///     "ConditionType": 3,
    ///     "value": "2024-12-31",
    ///     "value2": "2025-01-02",
    ///     "dataType": "datetime"
    ///   }
    /// ]
    /// ```
    /// </remarks>
    /// <param name="request">查詢條件與分頁設定</param>
    /// <returns>查詢結果</returns>
    [HttpPost(Routes.Search)]
    [ProducesResponseType(typeof(FormListDataViewModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public IActionResult GetForms([FromBody] FormSearchRequest? request)
    {
        try
        {
            if (request == null)
            {
                return BadRequest(new
                {
                    Error = "Request body is null",
                    Hint = "請確認傳入的 JSON 是否正確，至少需要提供查詢條件或分頁參數"
                });
            }

            var vm = _formService.GetFormList(_funcType, request);
            return Ok(vm);
        }
        catch (HttpStatusCodeException ex)
        {
            return StatusCode((int)ex.StatusCode, ex.Message);
        }
    }

    /// <summary>
    /// 取得編輯/檢視/新增資料表單
    /// </summary>
    /// <param name="formId">上隻 Api 取得的 BaseId</param>
    /// <param name="pk">上隻 Api 取得的 Pk，不傳為新增</param>
    /// <returns>回傳填寫表單的畫面</returns>
    [HttpPost(Routes.GetForm)]
    [ProducesResponseType(typeof(FormSubmissionViewModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public IActionResult GetForm([FromRoute] Guid formId, [FromQuery] string? pk)
    {
        try
        {
            if (formId == Guid.Empty)
            {
                return BadRequest(new { Detail = "formId 不可為空。" });
            }

            var vm = !string.IsNullOrWhiteSpace(pk)
                ? _formService.GetFormSubmission(formId, pk)
                : _formService.GetFormSubmission(formId);

            return Ok(vm);
        }
        catch (HttpStatusCodeException ex)
        {
            return StatusCode((int)ex.StatusCode, ex.Message);
        }
    }

    /// <summary>
    /// 刪除資料（先驗證 Guard 規則，通過才會進行物理刪除）
    /// </summary>
    /// <remarks>
    /// - 先依 FormFieldMasterId 撈 Guard 規則並逐條驗證
    /// - 任一規則不允許刪除 → 回 409 Conflict + BlockedByRule
    /// - 通過後才會刪除 Base Table 資料列
    /// </remarks>
    /// <param name="request">Guard 驗證參數</param>
    /// <param name="ct">取消權杖</param>
    [HttpPost(Routes.DeleteWithGuard)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteWithGuard([FromBody] DeleteWithGuardRequestViewModel? request, CancellationToken ct)
    {
        try
        {
            var badRequest = ValidateDeleteWithGuardRequest(request);
            if (badRequest != null)
            {
                return badRequest;
            }

            var result = await _formService.DeleteWithGuardAsync(request!, ct);

            if (!result.IsValid)
            {
                return BadRequest(new { Detail = result.ErrorMessage });
            }

            if (!result.CanDelete)
            {
                return Conflict(new { Detail = result.BlockedByRule });
            }

            if (!result.Deleted)
            {
                return NotFound(new { Detail = "找不到要刪除的資料。" });
            }

            return NoContent();
        }
        catch (HttpStatusCodeException ex)
        {
            return StatusCode((int)ex.StatusCode, ex.Message);
        }
    }

    /// <summary>
    /// 匯出 Excel
    /// </summary>
    [HttpPost(Routes.Export)]
    [Produces(MediaTypeNames.Application.Octet)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public IActionResult ExportForms([FromBody] FormSearchRequest? request)
    {
        try
        {
            if (request == null)
            {
                return BadRequest(new { Detail = "Request body is null" });
            }

            var file = _formService.ExportFormListToExcel(_funcType, request);
            return File(file.Content, file.ContentType, file.FileName);
        }
        catch (HttpStatusCodeException ex)
        {
            return StatusCode((int)ex.StatusCode, ex.Message);
        }
    }

    /// <summary>
    /// 提交表單
    /// </summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public IActionResult SubmitForm([FromBody] FormSubmissionInputModel input)
    {
        try
        {
            var rowId = _formService.SubmitForm(input);

            return Ok(new SubmitFormResponse
            {
                RowId = rowId.ToString()!,
                IsInsert = string.IsNullOrEmpty(input.Pk)
            });
        }
        catch (HttpStatusCodeException ex)
        {
            return StatusCode((int)ex.StatusCode, ex.Message);
        }
    }

    private IActionResult? ValidateDeleteWithGuardRequest(DeleteWithGuardRequestViewModel? request)
    {
        if (request == null)
        {
            return BadRequest(new { Detail = "請提供刪除驗證請求內容。" });
        }

        if (string.IsNullOrWhiteSpace(request.pk))
        {
            return BadRequest(new { Detail = "pk 不可為空。" });
        }

        if (request.FormFieldMasterId == Guid.Empty)
        {
            return BadRequest(new { Detail = "FormFieldMasterId 不可為空。" });
        }

        if (request.Parameters.Count == 0)
        {
            return BadRequest(new { Detail = "Parameters 不可為空。" });
        }

        return null;
    }
}

using ClassLibrary;
using DcMateH5Api.Areas.Form.Models;
using DcMateH5Api.Areas.Form.Interfaces;
using DcMateH5Api.Areas.Form.ViewModels;
using Microsoft.AspNetCore.Mvc;
using DcMateH5Api.Helper;

namespace DcMateH5Api.Areas.Form.Controllers;

/// <summary>
/// 表單主檔變更 API
/// </summary>
[Area("Form")]
[ApiController]
[ApiExplorerSettings(GroupName = SwaggerGroups.Form)]
[Route("[area]/[controller]")]
public class FormController : ControllerBase
{
    private readonly IFormService _formService;
    private readonly IFormDeleteGuardService _formDeleteGuardService;
    private readonly FormFunctionType _funcType = FormFunctionType.MasterMaintenance;
    
    public FormController(IFormService formService, IFormDeleteGuardService formDeleteGuardService)
    {
        _formService = formService;
        _formDeleteGuardService = formDeleteGuardService;
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
    [HttpPost("search")]
    [ProducesResponseType(typeof(FormListDataViewModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public IActionResult GetForms([FromBody] FormSearchRequest? request)
    {
        if (request == null)
        {
            return BadRequest(new
            {
                Error = "Request body is null",
                Hint  = "請確認傳入的 JSON 是否正確，至少需要提供查詢條件或分頁參數"
            });
        }
        
        var vm = _formService.GetFormList( _funcType, request );
        return Ok(vm);
    }
    
    /// <summary>
    /// 取得編輯/檢視/新增資料表單
    /// </summary>
    /// <param name="formId">上隻 Api 取得的 BaseId</param>
    /// <param name="pk">上隻 Api 取得的 Pk，不傳為新增</param>
    /// <returns>回傳填寫表單的畫面</returns>
    [HttpPost("{formId}")]
    [ProducesResponseType(typeof(FormSubmissionViewModel), StatusCodes.Status200OK)]
    public IActionResult GetForm(Guid formId, string? pk)
    {
        var vm = pk != null
            ? _formService.GetFormSubmission(formId, pk)
            : _formService.GetFormSubmission(formId);
        return Ok(vm);
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

        if (request.Parameters.Count == 0)
        {
            return BadRequest("Parameters 不可為空。");
        }

        var result = await _formDeleteGuardService.ValidateDeleteGuardAsync(request, ct);
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
    
    /// <summary>
    /// 物理刪除資料（依 BaseId + Pk）
    /// </summary>
    /// <remarks>
    /// - BaseId（formId）為上一支 search 回傳的 BaseId（BASE_TABLE_ID）
    /// - pk 為上一支 search 回傳的 Pk
    /// - 會刪除實際 Base Table 的資料列
    /// - 會同步清除 FORM_FIELD_DROPDOWN_ANSWER
    /// </remarks>
    /// <param name="formId">上隻 Api 取得的 BaseId（BASE_TABLE_ID）</param>
    /// <param name="pk">上隻 Api 取得的 Pk</param>
    /// <returns></returns>
    [HttpDelete("{formId}/{pk}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public IActionResult PhysicalDelete(Guid formId, string pk)
    {
        _formService.PhysicalDeleteByBaseTableId(formId, pk);
        return NoContent();
    }
    
    /// <summary>
    /// 提交表單
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public IActionResult SubmitForm([FromBody] FormSubmissionInputModel input)
    {
        _formService.SubmitForm(input);
        return NoContent();
    }
    
}

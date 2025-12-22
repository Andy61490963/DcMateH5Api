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
    private readonly FormFunctionType _funcType = FormFunctionType.MasterMaintenance;
    
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
    ///     "queryConditionType": 3,
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
    public IActionResult GetForm(Guid formId, string? pk)
    {
        var vm = pk != null
            ? _formService.GetFormSubmission(formId, pk)
            : _formService.GetFormSubmission(formId);
        return Ok(vm);
    }

    /// <summary>
    /// 提交表單
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    [HttpPost]
    public IActionResult SubmitForm([FromBody] FormSubmissionInputModel input)
    {
        _formService.SubmitForm(input);
        return NoContent();
    }

}

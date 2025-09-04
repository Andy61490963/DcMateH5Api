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

    public FormController(IFormService formService)
    {
        _formService = formService;
    }
    
    /// <summary>
    /// 範例輸入：
    /// <code>
    /// [
    ///   {
    ///     "column": "STATUS_CALCD_TIME",
    ///     "queryConditionType": 3,
    ///     "value": "2024-12-31",
    ///     "value2": "2025-01-02",
    ///     "dataType": "datetime"
    ///   }
    /// ]
    /// </code>
    /// </summary>
    /// <param name="conditions">查詢條件</param>
    /// <returns>查詢結果</returns>
    [HttpPost("search")]
    public IActionResult GetForms([FromBody] List<FormQueryCondition>? conditions)
    {
        var vm = _formService.GetFormList(conditions);
        return Ok(vm);
    }
    
    /// <summary>
    /// 取得編輯/檢視/新增資料表單
    /// </summary>
    /// <param name="formId">FORM_FIELD_Master.ID</param>
    /// <param name="pk">資料主鍵，不傳為新增</param>
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
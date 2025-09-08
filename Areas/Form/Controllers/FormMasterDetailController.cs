using DcMateH5Api.Areas.Form.Interfaces;
using DcMateH5Api.Areas.Form.Models;
using DcMateH5Api.Areas.Form.ViewModels;
using DcMateH5Api.Helper;
using Microsoft.AspNetCore.Mvc;

namespace DcMateH5Api.Areas.Form.Controllers;

/// <summary>
/// 主明細表單維護 API。
/// </summary>
[Area("Form")]
[ApiController]
[ApiExplorerSettings(GroupName = SwaggerGroups.FormWithMasterDetail)]
[Route("[area]/[controller]")]
public class FormMasterDetailController : ControllerBase
{
    private readonly IFormMasterDetailService _service;

    public FormMasterDetailController(IFormMasterDetailService service)
    {
        _service = service;
    }

    /// <summary>
    /// 取得主明細表單的資料列表。
    /// </summary>
    /// <param name="request">查詢條件與分頁設定。</param>
    [HttpPost("search")]
    public IActionResult GetForms([FromBody] FormSearchRequest? request)
    {
        var vm = _service.GetFormList(request);
        return Ok(vm);
    }

    /// <summary>
    /// 取得主表與明細表的編輯/檢視/新增資料表單。
    /// </summary>
    /// <param name="formId">主明細表頭的 FORM_FIELD_Master.ID。</param>
    /// <param name="pk">主表資料主鍵，不傳為新增。</param>
    [HttpPost("{formId}")]
    public IActionResult GetForm(Guid formId, string? pk)
    {
        var vm = _service.GetFormSubmission(formId, pk);
        return Ok(vm);
    }

    /// <summary>
    /// 提交主表與明細表資料。
    /// </summary>
    /// <param name="input">提交資料。</param>
    [HttpPost]
    public IActionResult SubmitForm([FromBody] FormMasterDetailSubmissionInputModel input)
    {
        _service.SubmitForm(input);
        return NoContent();
    }
}

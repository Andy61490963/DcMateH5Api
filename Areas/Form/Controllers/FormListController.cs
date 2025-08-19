using DynamicForm.Areas.Form.Models;
using DynamicForm.Areas.Form.Interfaces;
using Microsoft.AspNetCore.Mvc;
using DynamicForm.Helper;

namespace DynamicForm.Areas.Form.Controllers;

/// <summary>
/// 表單主檔列表 API
/// </summary>
[Area("Form")]
[ApiController]
[ApiExplorerSettings(GroupName = SwaggerGroups.Form)]
[Route("[area]/[controller]")]
public class FormListController : ControllerBase
{
    private readonly IFormListService _service;

    /// <summary>
    /// 建構式注入 FormListService。
    /// </summary>
    /// <param name="service">表單清單服務介面</param>
    public FormListController(IFormListService service)
    {
        _service = service;
    }

    /// <summary>
    /// 取得所有表單主檔清單，可透過關鍵字進行模糊搜尋。
    /// </summary>
    /// <param name="q">可選的搜尋關鍵字，將比對 FORM_NAME</param>
    /// <returns>符合條件的表單主檔列表</returns>
    [HttpGet]
    public IActionResult GetFormMasters(string? q)
    {
        var list = _service.GetFormMasters();
        if (!string.IsNullOrWhiteSpace(q))
        {
            list = list
                .Where(x => x.FORM_NAME.Contains(q, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
        return Ok(list);
    }

    /// <summary>
    /// 刪除指定的表單主檔資料。
    /// </summary>
    /// <param name="id">FORM_FIELD_Master 的唯一識別編號</param>
    /// <returns>NoContent 回應</returns>
    [HttpDelete("{id}")]
    public IActionResult Delete(Guid id)
    {
        _service.DeleteFormMaster(id);
        return NoContent();
    }
}
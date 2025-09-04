using System;
using DcMateH5Api.Areas.Form.Interfaces;
using DcMateH5Api.Areas.Form.ViewModels;
using Microsoft.AspNetCore.Mvc;
using DcMateH5Api.Helper;

namespace DcMateH5Api.Areas.Form.Controllers;

[Area("Form")]
[ApiController]
[ApiExplorerSettings(GroupName = SwaggerGroups.Form)]
[Route("[area]/[controller]")]
[Produces("application/json")]
public class FormDesignerMasterDetailController : ControllerBase
{
    private readonly IFormDesignerService _formDesignerService;

    public FormDesignerMasterDetailController(IFormDesignerService formDesignerService)
    {
        _formDesignerService = formDesignerService;
    }

    /// <summary>
    /// 儲存 Master/Detail 表單主檔資訊
    /// </summary>
    [HttpPost("headers")]
    public IActionResult SaveMasterDetailFormHeader([FromBody] MasterDetailFormHeaderViewModel model)
    {
        if (model.MASTER_TABLE_ID == Guid.Empty ||
            model.DETAIL_TABLE_ID == Guid.Empty ||
            model.VIEW_TABLE_ID == Guid.Empty)
        {
            return BadRequest("MASTER_TABLE_ID / DETAIL_TABLE_ID / VIEW_TABLE_ID 不可為空");
        }

        if (_formDesignerService.CheckMasterDetailFormMasterExists(
                model.MASTER_TABLE_ID,
                model.DETAIL_TABLE_ID,
                model.VIEW_TABLE_ID,
                model.ID))
        {
            return Conflict("相同的 Master/Detail/View 組合已存在");
        }

        var id = _formDesignerService.SaveMasterDetailFormHeader(model);
        return Ok(new { id });
    }
}

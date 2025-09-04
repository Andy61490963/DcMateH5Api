using System;
using ClassLibrary;
using DcMateH5Api.Areas.Form.Interfaces;
using DcMateH5Api.Areas.Form.Models;
using DcMateH5Api.Areas.Form.ViewModels;
using Microsoft.AspNetCore.Mvc;
using DcMateH5Api.Helper;

namespace DcMateH5Api.Areas.Form.Controllers;

[Area("Form")]
[ApiController]
[ApiExplorerSettings(GroupName = SwaggerGroups.FormWithMasterDetail)]
[Route("[area]/[controller]")]
[Produces("application/json")]
public class FormDesignerMasterDetailController : ControllerBase
{
    private readonly IFormDesignerService _formDesignerService;
    private readonly FormFunctionType _funcType = FormFunctionType.MasterDetail;
    
    public FormDesignerMasterDetailController(IFormDesignerService formDesignerService)
    {
        _formDesignerService = formDesignerService;
    }

    // ────────── Form Designer 列表 ──────────
    /// <summary>
    /// 取得表單主檔 (FORM_FIELD_Master) 清單
    /// </summary>
    /// <param name="q">關鍵字 (模糊搜尋 FORM_NAME)</param>
    /// <param name="ct">CancellationToken</param>
    /// <returns>表單主檔清單</returns>
    [HttpGet]
    public async Task<ActionResult<List<FORM_FIELD_Master>>> GetFormMasters(
        [FromQuery] string? q,
        CancellationToken ct)
    {
        var masters = await _formDesignerService.GetFormMasters(_funcType, q, ct);

        return Ok(masters.ToList());
    }
    
    /// <summary>
    /// 更新主檔 or 明細 or 檢視表 名稱
    /// </summary>
    [HttpPut("form-name")]
    public async Task<IActionResult> UpdateFormName([FromBody] UpdateFormNameViewModel model, CancellationToken ct)
    {
        await _formDesignerService.UpdateFormName(model.ID, model.FORM_NAME, ct);
        return Ok();
    }
    
    /// <summary>
    /// 刪除指定的主檔 or 明細 or 檢視表資料
    /// </summary>
    /// <param name="id">FORM_FIELD_Master 的唯一識別編號</param>
    /// <returns>NoContent 回應</returns>
    [HttpDelete("{id}")]
    public IActionResult Delete(Guid id)
    {
        _formDesignerService.DeleteFormMaster(id);
        return NoContent();
    }
    
    // ────────── Form Designer 入口 ──────────
    /// <summary>
    /// 取得指定的 主檔 and 明細 and 檢視表 主畫面資料(請傳入父節點 masterId)
    /// </summary>
    // [RequirePermission(ActionAuthorizeHelper.View)]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetDesigner(Guid id, CancellationToken ct)
    {
        var model = await _formDesignerService.GetFormDesignerIndexViewModel(_funcType, id, ct);
        return Ok(model);
    }
    
    /// <summary>
    /// 取得資料表所有欄位設定(如果傳入空formMasterId，會創建一筆新的，如果有傳入，會取得舊的)
    /// </summary>
    [HttpGet("tables/{tableName}/fields")]
    public IActionResult GetFields(string tableName, Guid? formMasterId, [FromQuery] TableSchemaQueryType schemaType)
    {
        try
        {
            var result = _formDesignerService.EnsureFieldsSaved(tableName, formMasterId, schemaType);

            if (result == null)
            {
                return NotFound();
            }
            return Ok(result);
        }
        catch (HttpStatusCodeException ex)
        {
            return StatusCode((int)ex.StatusCode, ex.Message);
        }
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

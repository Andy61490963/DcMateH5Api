using ClassLibrary;
using DcMateH5Api.Areas.Form.Interfaces;
using DcMateH5Api.Areas.Form.Models;
using DcMateH5Api.Areas.Form.ViewModels;
using DcMateH5Api.Controllers;
using DcMateH5Api.Helper;
using Microsoft.AspNetCore.Mvc;

namespace DcMateH5Api.Areas.Form.Controllers;

[Area("Form")]
[ApiController]
[ApiExplorerSettings(GroupName = SwaggerGroups.FormTableValueFunction)]
[Route("[area]/[controller]")]
public class FormTableValueFunctionController : BaseController
{
    private readonly IFormService _formService;
    private readonly IFormTableValueFunctionService _formTableValueFunctionService;
    private readonly FormFunctionType _funcType = FormFunctionType.TableValueFunctionMaintenance;

    public FormTableValueFunctionController(IFormService formService, IFormTableValueFunctionService formTableValueFunctionService)
    {
        _formService = formService;
        _formTableValueFunctionService = formTableValueFunctionService;
    }
    
    private static class Routes
    {
        public const string Masters = "masters";
        public const string Search = "search";
    }
    
    /// <summary>
    /// 取得多對多設定檔清單，供前端呈現可選的維護方案。
    /// </summary>
    [HttpGet(Routes.Masters)]
    [ProducesResponseType(typeof(IEnumerable<TableValueFunctionConfigViewModel>), StatusCodes.Status200OK)]
    public ActionResult<IEnumerable<TableValueFunctionConfigViewModel>> GetFormMasters(CancellationToken ct)
    {
        try
        {
            var masters = _formTableValueFunctionService.GetFormMasters(ct);
            return Ok(masters);
        }
        catch (HttpStatusCodeException ex)
        {
            return StatusCode((int)ex.StatusCode, ex.Message);
        }
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
    public IActionResult GetForms([FromBody] FormTvfSearchRequest? request)
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

            var vm = _formTableValueFunctionService.GetTvfFormList(_funcType, request);
            return Ok(vm);
        }
        catch (HttpStatusCodeException ex)
        {
            return StatusCode((int)ex.StatusCode, ex.Message);
        }
    }
}
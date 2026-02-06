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
    private readonly IFormTableValueFunctionService _formTableValueFunctionService;
    private readonly FormFunctionType _funcType = FormFunctionType.TableValueFunctionMaintenance;

    public FormTableValueFunctionController(IFormTableValueFunctionService formTableValueFunctionService)
    {
        _formTableValueFunctionService = formTableValueFunctionService;
    }

    private static class Routes
    {
        public const string Masters = "masters";
        public const string Search = "search";
    }

    /// <summary>
    /// 取得 Table Value Function 設定檔清單。
    /// </summary>
    [HttpGet(Routes.Masters)]
    [ProducesResponseType(typeof(IEnumerable<TableValueFunctionConfigViewModel>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<TableValueFunctionConfigViewModel>>> GetFormMasters(CancellationToken ct)
    {
        try
        {
            var masters = await _formTableValueFunctionService.GetFormMasters(ct);
            return Ok(masters);
        }
        catch (HttpStatusCodeException ex)
        {
            return StatusCode((int)ex.StatusCode, ex.Message);
        }
    }

    /// <summary>
    /// 取得 Table Value Function 維護的資料列表
    /// </summary>
    /// <remarks>
    /// ### 使用說明
    ///
    /// 此 API 用於查詢 **Table-Valued Function（TVF）維護清單**，支援：
    /// - TVF 參數（`TvfParameters`）
    /// - 回傳欄位的條件查詢（`Conditions`）
    /// - 排序（`OrderBys`）
    /// - 分頁（`Page` / `PageSize`）
    /// ### 範例輸入
    /// ```json
    /// {
    ///   "FormMasterId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    ///   "Page": 0,
    ///   "PageSize": 10,
    ///   "Conditions": [
    ///     {
    ///       "Column": "EQP_NO",
    ///       "ConditionType": 1,
    ///       "Value": "MC1",
    ///       "DataType": "string"
    ///     }
    ///   ],
    ///   "OrderBys": [
    ///     {
    ///       "Column": "EQP_NAME",
    ///       "Direction": 1
    ///     }
    ///   ],
    ///   "TvfParameters": {
    ///     "EQP_NO": "MC1",
    ///     "S_TIME": "2025-12-15 08:00:00",
    ///     "E_TIME": "2026-01-30 08:00:00"
    ///   }
    /// }
    /// ```
    /// </remarks>
    /// <param name="request">查詢條件與分頁設定</param>
    /// <param name="ct">CancellationToken</param>
    /// <returns>查詢結果</returns>
    [HttpPost(Routes.Search)]
    [ProducesResponseType(typeof(FormListResponseViewModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetForms([FromBody] FormTvfSearchRequest? request, CancellationToken ct)
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

            var vm = await _formTableValueFunctionService.GetTvfFormList(_funcType, request, ct);
            return Ok(vm);
        }
        catch (HttpStatusCodeException ex)
        {
            return StatusCode((int)ex.StatusCode, ex.Message);
        }
    }
}
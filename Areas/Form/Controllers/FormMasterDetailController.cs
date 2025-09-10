using ClassLibrary;
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
    private readonly FormFunctionType _funcType = FormFunctionType.MasterDetail;
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
        if (request == null)
        {
            return BadRequest(new
            {
                Error = "Request body is null",
                Hint  = "請確認傳入的 JSON 是否正確，至少需要提供查詢條件或分頁參數"
            });
        }
        var vm = _service.GetFormList( _funcType, request );
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
    /// 提交主表與明細表資料
    /// </summary>
    /// <remarks>
    /// ### 使用說明
    ///
    /// 此 API 用於提交主檔與明細檔資料，規則如下：  
    ///
    /// 1. **RelationColumn**（例如 `TOL_NO`）是主從關聯欄位，名稱由設定推得，不一定叫 `TOL_NO`。  
    /// 2. **新增主檔**：當 `MasterPk` 為空時，必須在 `MasterFields` 帶入 RelationColumn 的 `FieldConfigId` 與 `Value`；
    ///    系統不會自動產生 Relation 值，缺少就會報錯。  
    /// 3. **新增/更新判斷**：  
    ///    - 主檔：`MasterPk` 有值 → 更新（僅當 `MasterFields` 有欄位才會更新）；`MasterPk` 空 → 新增。  
    ///    - 明細：每筆 `Detail.Pk` 獨立判斷；空 → 新增，有值 → 更新（可混搭）。  
    /// 4. **一致性**：在寫入任何明細前，系統會強制將明細的 RelationColumn 覆蓋為主檔的 Relation 值；
    ///    即使前端送不同值也會被覆蓋，避免明細被綁錯主檔。  
    /// 5. **設定要求**：請確保「明細的 RelationColumn」在 `FORM_FIELD_CONFIG` 中設為 `IS_EDITABLE = 1`；
    ///    否則該欄位會在單表提交時被忽略，導致無法寫入 FK。  
    ///
    /// ### 範例請求 (Version 1.0.0)
    /// 下列 JSON 範例展示了新增主檔及兩筆明細的提交格式：  
    ///
    /// ```json
    /// {
    ///   "BaseId": "3FA85F64-5717-4562-B3FC-2C963F66AFA1",
    ///   "MasterPk": "",
    ///   "MasterFields": [
    ///     {
    ///       "FieldConfigId": "7ebcfe8b-ddad-4170-aa28-5f0762efafc3",
    ///       "Value": "測試新增TOL_NAME"
    ///     },
    ///     {
    ///       "FieldConfigId": "AE2312F9-17FB-4A9D-B7AE-EC933A447D50",
    ///       "Value": "關聯欄位(這邊是TOL_NO)"
    ///     }
    ///   ],
    ///   "DetailRows": [
    ///     {
    ///       "Pk": "",
    ///       "Fields": [
    ///         {
    ///           "FieldConfigId": "0d42dba7-9afb-4d80-b440-c9e6dcb1cc03",
    ///           "Value": "測試新增TOL_DETALS_NAME1"
    ///         }
    ///       ]
    ///     },
    ///     {
    ///       "Pk": "",
    ///       "Fields": [
    ///         {
    ///           "FieldConfigId": "0d42dba7-9afb-4d80-b440-c9e6dcb1cc03",
    ///           "Value": "測試新增TOL_DETALS_NAME2"
    ///         }
    ///       ]
    ///     }
    ///   ]
    /// }
    /// ```
    /// </remarks>
    [HttpPost]
    public IActionResult SubmitForm([FromBody] FormMasterDetailSubmissionInputModel input)
    {
        _service.SubmitForm(input);
        return NoContent();
    }
}

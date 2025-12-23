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
    private const int DefaultDetailPage = 1;
    private const int DefaultDetailPageSize = 5;

    private readonly IFormMasterDetailService _service;
    private readonly FormFunctionType _funcType = FormFunctionType.MasterDetailMaintenance;
    public FormMasterDetailController(IFormMasterDetailService service)
    {
        _service = service;
    }

    /// <summary>
    /// 取得主明細表單的資料列表。
    /// </summary>
    /// <remarks>
    /// ### 使用說明
    /// 
    /// 此 API 用於查詢指定表單的資料列，支援條件篩選與分頁功能。  
    /// 
    /// 1. **查詢條件 (`Conditions`)**  
    ///    - 每個條件包含 `Column`、`ConditionType`、`Value/Value2/Values`、`DataType`。  
    ///    - `ConditionType` 對應 SQL 運算子（見下方表格）。  
    ///    - `DataType` 用於轉換正確的 SQL 資料型別（例如 `nvarchar`、`datetime`）。  
    /// 
    /// 2. **分頁控制**  
    ///    - `Page`：頁碼（從 1 開始），預設 1。  
    ///    - `PageSize`：每頁筆數，預設 20。  
    /// 
    /// 3. **欄位轉換**  
    ///    - 下拉選單（Dropdown）欄位會自動將選項代號轉換成顯示文字（OptionText）。  
    /// 
    /// 4. **表單選擇**  
    ///    - 若指定 `FormMasterId`，則僅查詢該表單。  
    ///    - 若為空，則依系統的 `funcType` 設定查詢所有可用表單。  
    /// 
    /// 5. **錯誤處理**  
    ///    - 若請求內容為 `null`，回傳 `400 Bad Request`，並附帶錯誤提示與建議。  
    /// 
    /// ### ConditionType 對照表
    /// <table>
    ///   <tr><th>數值</th><th>名稱 (Enum)</th><th>顯示名稱</th><th>對應 SQL 運算子</th></tr>
    ///   <tr><td>0</td><td>None</td><td>無</td><td>(不套用條件)</td></tr>
    ///   <tr><td>1</td><td>Equal</td><td>等於</td><td>=</td></tr>
    ///   <tr><td>2</td><td>Like</td><td>包含</td><td>LIKE '%value%'</td></tr>
    ///   <tr><td>3</td><td>Between</td><td>區間</td><td>BETWEEN v1 AND v2</td></tr>
    ///   <tr><td>4</td><td>GreaterThan</td><td>大於</td><td>&gt;</td></tr>
    ///   <tr><td>5</td><td>GreaterThanOrEqual</td><td>大於等於</td><td>&gt;=</td></tr>
    ///   <tr><td>6</td><td>LessThan</td><td>小於</td><td>&lt;</td></tr>
    ///   <tr><td>7</td><td>LessThanOrEqual</td><td>小於等於</td><td>&lt;=</td></tr>
    ///   <tr><td>8</td><td>In</td><td>包含於</td><td>IN (...)</td></tr>
    ///   <tr><td>9</td><td>NotEqual</td><td>不等於</td><td>&lt;&gt;</td></tr>
    ///   <tr><td>10</td><td>NotIn</td><td>不包含於</td><td>NOT IN (...)</td></tr>
    /// </table>
    /// 
    /// ### 範例請求 (Version 1.0.0)
    /// ```json
    /// {
    ///   "FormMasterId": "3FA85F64-5717-4562-B3FC-2C963F66AFA1",
    ///   "Page": 1,
    ///   "PageSize": 10,
    ///   "Conditions": [
    ///     {
    ///       "Column": "CREATE_TIME",
    ///       "ConditionType": 3,
    ///       "Value": "2025-01-01",
    ///       "Value2": "2025-12-31",
    ///       "DataType": "datetime"
    ///     },
    ///     {
    ///       "Column": "TOL_NO",
    ///       "ConditionType": 8,
    ///       "Values": ["FGM-L177-07600", "FGM-L000-00500"],
    ///       "DataType": "nvarchar"
    ///     },
    ///     {
    ///       "Column": "TOL_NAME",
    ///       "ConditionType": 2,
    ///       "Value": "FOAM BLOCK 25A",
    ///       "DataType": "nvarchar"
    ///     }
    ///   ]
    /// }
    /// ```
    /// 
    /// ### 範例回應 (簡化)
    /// ```json
    /// [
    ///   {
    ///     "FormMasterId": "3FA85F64-5717-4562-B3FC-2C963F66AFA1",
    ///     "BaseId": "A1112222-3333-4444-5555-666677778888",
    ///     "Pk": "12345",
    ///     "Fields": [
    ///       {
    ///         "Column": "TOL_NAME",
    ///         "CurrentValue": "FOAM BLOCK 25A"
    ///       },
    ///       {
    ///         "Column": "STATUS",
    ///         "CurrentValue": "Active"
    ///       }
    ///     ]
    ///   }
    /// ]
    /// ```
    /// </remarks>
    [HttpPost("search")]
    [ProducesResponseType(typeof(FormListDataViewModel), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
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
    /// <param name="formId">主明細表頭的 FORM_FIELD_MASTER.ID。</param>
    /// <param name="pk">主表資料主鍵，不傳為新增。</param>
    [HttpPost("{formId}")]
    [ProducesResponseType(typeof(FormMasterDetailSubmissionViewModel), StatusCodes.Status200OK)]
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
    /// 2. **新增主檔**：當 `MasterPk` 為空時，不一定要在 `MasterFields` 帶入 RelationColumn 的 `FieldConfigId` 與 `Value`；
    ///    設定時，缺少 Relation 值，像是主明細應該要都有的 `TOL_NO` ，就會報錯。
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
    ///   "MasterId": "3FA85F64-5717-4562-B3FC-2C963F66AFA1",
    ///   "MasterPk": "",
    ///   "MasterFields": [
    ///     {
    ///       "FieldConfigId": "7ebcfe8b-ddad-4170-aa28-5f0762efafc3",
    ///       "Value": "測試新增TOL_NAME"
    ///     },
    ///     {
    ///       "FieldConfigId": "AE2312F9-17FB-4A9D-B7AE-EC933A447D50",
    ///       "Value": "關聯欄位(這邊是TOL_NO)，如果沒沒傳沒關係，系統會自己分析查找，但如果有傳，且更改了值，可能會造成主明細關聯脫鉤"
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
    ///       "Pk": "5530380073504692",
    ///       "Fields": [
    ///         {
    ///           "FieldConfigId": "27c4e233-0b96-4608-8a75-9223f54f8a1c",
    ///           "Value": "修改明細表指向特定主檔 關聯欄位(這邊是TOL_NO)"
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
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult SubmitForm([FromBody] FormMasterDetailSubmissionInputModel input)
    {
        _service.SubmitForm(input);
        return NoContent();
    }
}

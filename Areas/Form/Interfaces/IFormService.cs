using DcMateH5Api.Areas.Form.Models;
using DcMateH5Api.Areas.Form.ViewModels;
using ClassLibrary;
using DcMateH5Api.Areas.Form.Models.Excel;
using Microsoft.Data.SqlClient;

namespace DcMateH5Api.Areas.Form.Interfaces;

public interface IFormService
{
    /// <summary>
    /// 取得所有表單的資料列表，支援分頁。
    /// </summary>
    /// <param name="funcType">功能類型</param>
    /// <param name="request">查詢條件與分頁設定。</param>
    /// <returns>每個表單對應的欄位與資料列集合。</returns>
    List<FormListDataViewModel> GetFormList(FormFunctionType funcType, FormSearchRequest? request = null, bool returnBaseTableWhenMultipleMapping = false);
    
    /// <summary>
    /// 取得 單一
    /// </summary>
    /// <param name="id"></param>
    /// <param name="pk"></param>
    /// <returns></returns>
    FormSubmissionViewModel GetFormSubmission(Guid? id, string? pk = null);

    /// <summary>
    /// 驗證規則 + 刪除
    /// </summary>
    /// <param name="request"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task<DeleteWithGuardResultViewModel> DeleteWithGuardAsync(
        DeleteWithGuardRequestViewModel request,
        CancellationToken ct);
    
    ExportFileResult ExportFormListToExcel(FormFunctionType funcType, FormSearchRequest request);
    
    /// <summary>
    /// 儲存或更新表單資料
    /// </summary>
    object SubmitForm(FormSubmissionInputModel input);

    /// <summary>
    /// 儲存或更新表單資料，允許呼叫端提供交易物件以便進行複合交易控制。
    /// </summary>
    /// <param name="input">前端送出的表單資料</param>
    /// <param name="tx">資料庫交易物件</param>
    object SubmitForm(FormSubmissionInputModel input, SqlTransaction tx);
    
    List<FormFieldInputViewModel> GetFieldTemplates(
        Guid? masterId,
        TableSchemaQueryType schemaType,
        string tableName);
}
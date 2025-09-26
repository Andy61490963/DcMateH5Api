using ClassLibrary;
using DcMateH5Api.Areas.Form.Models;
using DcMateH5Api.Areas.Form.ViewModels;

namespace DcMateH5Api.Areas.Form.Interfaces;

/// <summary>
/// 提供主明細表單的 CRUD 服務介面。
/// </summary>
public interface IFormMasterDetailService
{
    /// <summary>
    /// 取得主明細表單的資料列表。
    /// </summary>
    /// <param name="funcType">功能類型</param>
    /// <param name="request">查詢條件與分頁設定。</param>
    /// <returns>表單資料列表。</returns>
    List<FormListDataViewModel> GetFormList(FormFunctionType funcType, FormSearchRequest? request = null);

    /// <summary>
    /// 取得主表與明細表的填寫畫面與資料。
    /// </summary>
    /// <param name="formMasterDetailId">主明細表單的 FORM_FIELD_Master.ID</param>
    /// <param name="pk">主表資料主鍵，不傳為新增。</param>
    /// <returns>主表與明細表的填寫資料。</returns>
    FormMasterDetailSubmissionViewModel GetFormSubmission(Guid formMasterDetailId, string? pk = null);

    /// <summary>
    /// 新增或更新主表與明細表資料。
    /// </summary>
    /// <param name="input">主明細表單的提交資料。</param>
    void SubmitForm(FormMasterDetailSubmissionInputModel input);

    /// <summary>
    /// 依分頁取得指定主明細設定下的明細資料列。
    /// </summary>
    /// <param name="formMasterDetailId">主明細表單的 FORM_FIELD_Master.ID。</param>
    /// <param name="page">頁碼（從 1 起算）。</param>
    /// <param name="pageSize">每頁筆數。</param>
    /// <returns>分頁後的明細列資料。</returns>
    FormDetailRowPageViewModel GetDetailRows(Guid formMasterDetailId, int page, int pageSize);
}

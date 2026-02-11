using DcMateClassLibrary.Enums.Form;
using DcMateH5.Abstractions.Form.ViewModels;

namespace DcMateH5.Abstractions.Form.Form;

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
    Task<List<FormListResponseViewModel>> GetFormListAsync(FormFunctionType funcType, FormSearchRequest? request = null, CancellationToken ct = default);

    /// <summary>
    /// 取得主表與明細表的填寫畫面與資料。
    /// </summary>
    /// <param name="formMasterDetailId">主明細表單的 FORM_FIELD_MASTER.ID</param>
    /// <param name="pk">主表資料主鍵，不傳為新增。</param>
    /// <returns>主表與明細表的填寫資料。</returns>
    Task<FormMasterDetailSubmissionViewModel> GetFormSubmissionAsync(Guid formMasterDetailId, string? pk = null, CancellationToken ct = default);

    /// <summary>
    /// 新增或更新主表與明細表資料。
    /// </summary>
    /// <param name="input">主明細表單的提交資料。</param>
    Task SubmitFormAsync(FormMasterDetailSubmissionInputModel input, CancellationToken ct = default);
}

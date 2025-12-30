using DcMateH5Api.Areas.Form.ViewModels;

namespace DcMateH5Api.Areas.Form.Interfaces;

public interface IFormDeleteGuardService
{
    /// <summary>
    /// 驗證刪除守門規則，依序執行 Guard SQL 並回傳是否可刪除。
    /// </summary>
    /// <param name="request">刪除驗證請求內容</param>
    /// <param name="ct">取消權杖</param>
    /// <returns>驗證結果，包含可否刪除與阻擋原因</returns>
    Task<DeleteGuardValidateResultViewModel> ValidateDeleteGuardAsync(
        DeleteGuardValidateRequestViewModel request,
        CancellationToken ct = default);
}

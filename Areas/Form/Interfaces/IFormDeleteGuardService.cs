using DcMateH5Api.Areas.Form.ViewModels;
using Microsoft.Data.SqlClient;

namespace DcMateH5Api.Areas.Form.Interfaces;

public interface IFormDeleteGuardService
{
    /// <summary>
    /// 驗證刪除守門規則，依序執行 Guard SQL 並回傳是否可刪除。
    /// </summary>
    /// <param name="formFieldMasterId"></param>
    /// <param name="parameters"></param>
    /// <param name="tx"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task<DeleteGuardValidateResultViewModel> ValidateDeleteGuardInternalAsync(
        Guid formFieldMasterId,
        Dictionary<string, string> parameters,
        SqlTransaction tx,
        CancellationToken ct);
}

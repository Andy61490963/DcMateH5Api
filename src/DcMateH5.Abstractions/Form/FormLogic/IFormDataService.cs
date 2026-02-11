using DcMateH5.Abstractions.Form.ViewModels;

namespace DcMateH5.Abstractions.Form.FormLogic;

public interface IFormDataService
{
    Task<List<IDictionary<string, object?>>> GetRowsAsync(
        string tableName,
        IEnumerable<FormQueryConditionViewModel>? conditions = null,
        IEnumerable<FormOrderBy>? orderBys = null,
        int? page = null,
        int? pageSize = null,
        CancellationToken ct = default);

    Task<int> GetTotalCountAsync(
        string tableName,
        IEnumerable<FormQueryConditionViewModel>? conditions = null,
        CancellationToken ct = default);

    Task<Dictionary<string, string>> LoadColumnTypesAsync(string tableName, CancellationToken ct = default);

    // 同步方法（僅供既有模組遷移期間使用）
    List<IDictionary<string, object?>> GetRows(
        string tableName,
        IEnumerable<FormQueryConditionViewModel>? conditions = null,
        IEnumerable<FormOrderBy>? orderBys = null,
        int? page = null,
        int? pageSize = null);

    int GetTotalCount(string tableName, IEnumerable<FormQueryConditionViewModel>? conditions = null);

    Dictionary<string, string> LoadColumnTypes(string tableName);
}

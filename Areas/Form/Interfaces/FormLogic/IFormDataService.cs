using DcMateH5Api.Areas.Form.Models;
using ClassLibrary;

namespace DcMateH5Api.Areas.Form.Interfaces.FormLogic;

public interface IFormDataService
{
    /// <summary>
    /// 取得資料列，允許依條件過濾。
    /// </summary>
    /// <param name="tableName">目標資料表或檢視表名稱。</param>
    /// <param name="conditions">查詢條件集合。</param>
    /// <param name="orderBys">排序。</param>
    /// <param name="page">頁碼（從 1 開始）。</param>
    /// <param name="pageSize">每頁筆數。</param>
    /// <returns>符合條件的資料列。</returns>
    List<IDictionary<string, object?>> GetRows(
        string tableName,
        IEnumerable<FormQueryConditionViewModel>? conditions = null,
        IEnumerable<FormOrderBy>? orderBys = null,
        int? page = null,
        int? pageSize = null);

    int GetTotalCount(string tableName, IEnumerable<FormQueryConditionViewModel>? conditions = null);

    Dictionary<string, string> LoadColumnTypes(string tableName);
}
using DynamicForm.Areas.Form.Models;
using ClassLibrary;

namespace DynamicForm.Areas.Form.Interfaces.FormLogic;

public interface IFormDataService
{
    /// <summary>
    /// 取得資料列，允許依條件過濾。
    /// </summary>
    /// <param name="tableName">目標資料表或檢視表名稱。</param>
    /// <param name="conditions">查詢條件集合。</param>
    /// <returns>符合條件的資料列。</returns>
    List<IDictionary<string, object?>> GetRows(string tableName, IEnumerable<FormQueryCondition>? conditions = null);

    Dictionary<string, string> LoadColumnTypes(string tableName);
}
using DynamicForm.Areas.Form.Models;
using ClassLibrary;
using Microsoft.Data.SqlClient;

namespace DynamicForm.Areas.Form.Interfaces.FormLogic;

public interface ISchemaService
{
    List<string> GetFormFieldMaster(string table);

    string? GetPrimaryKeyColumn(string tableName);
    HashSet<string> GetPrimaryKeyColumns(string tableName);

    (string PkName, string PkType, object? Value) ResolvePk(string tableName, string? rawId, SqlTransaction? tx = null);

    bool IsIdentityColumn(string tableName, string columnName, SqlTransaction? tx = null);
}
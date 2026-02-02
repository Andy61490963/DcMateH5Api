using DcMateH5Api.Areas.Form.Models;
using ClassLibrary;
using DcMateH5Api.Areas.Form.ViewModels;
using Microsoft.Data.SqlClient;

namespace DcMateH5Api.Areas.Form.Interfaces.FormLogic;

public interface ISchemaService
{
    List<string> GetFormFieldMaster(string table, SqlTransaction? tx = null);

    string? GetPrimaryKeyColumn(string tableName);
    HashSet<string> GetPrimaryKeyColumns(string tableName);

    (string PkName, string PkType, object? Value) ResolvePk(string tableName, string? rawId, SqlTransaction? tx = null);

    bool IsIdentityColumn(string tableName, string columnName, SqlTransaction? tx = null);
    
    string GetTableNameByTableId(Guid tableId, SqlTransaction? tx = null);

    Task<List<DbColumnInfo>> GetObjectSchemaInTxAsync(SqlConnection conn, SqlTransaction tx, string schemaName,
        string objectName, CancellationToken ct);
}
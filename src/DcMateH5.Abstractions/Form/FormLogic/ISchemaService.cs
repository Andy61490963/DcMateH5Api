using DcMateH5.Abstractions.Form.ViewModels;
using Microsoft.Data.SqlClient;

namespace DcMateH5.Abstractions.Form.FormLogic;

public interface ISchemaService
{
    Task<List<string>> GetFormFieldMasterAsync(string table, SqlTransaction? tx = null, CancellationToken ct = default);
    Task<string?> GetPrimaryKeyColumnAsync(string tableName, CancellationToken ct = default);
    Task<HashSet<string>> GetPrimaryKeyColumnsAsync(string tableName, CancellationToken ct = default);
    Task<(string PkName, string PkType, object? Value)> ResolvePkAsync(string tableName, string? rawId, SqlTransaction? tx = null, CancellationToken ct = default);
    Task<bool> IsIdentityColumnAsync(string tableName, string columnName, SqlTransaction? tx = null, CancellationToken ct = default);
    Task<string> GetTableNameByTableIdAsync(Guid tableId, SqlTransaction? tx = null, CancellationToken ct = default);

    // 相容舊呼叫端（逐步淘汰）
    List<string> GetFormFieldMaster(string table, SqlTransaction? tx = null);
    string? GetPrimaryKeyColumn(string tableName);
    HashSet<string> GetPrimaryKeyColumns(string tableName);
    (string PkName, string PkType, object? Value) ResolvePk(string tableName, string? rawId, SqlTransaction? tx = null);
    bool IsIdentityColumn(string tableName, string columnName, SqlTransaction? tx = null);
    string GetTableNameByTableId(Guid tableId, SqlTransaction? tx = null);

    Task<List<DbColumnInfo>> GetObjectSchemaInTxAsync(SqlConnection conn, SqlTransaction tx, string schemaName,
        string objectName, CancellationToken ct);
}

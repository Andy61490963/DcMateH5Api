using System.Data;
using Microsoft.Data.SqlClient;

/// <summary>
/// Provides high level CRUD operations by delegating SQL generation to <see cref="ICrudSqlBuilder"/>
/// and execution to <see cref="IDbExecutor"/>.
/// </summary>
public interface ICrudService
{
    Task<int> InsertAsync(string table, object dto, CancellationToken ct = default);
    Task<T?> InsertOutputAsync<T>(string table, object dto, string identityColumn, CancellationToken ct = default);
    Task<int> UpdateAsync(string table, object setDto, object whereDto, CancellationToken ct = default);
    Task<int> DeleteAsync(string table, object whereDto, CancellationToken ct = default);
    Task<bool> ExistsAsync(string table, object whereDto, CancellationToken ct = default);

    Task<int> InsertAsync(SqlConnection conn, SqlTransaction? tx, string table, object dto, CancellationToken ct = default);
    Task<T?> InsertOutputAsync<T>(SqlConnection conn, SqlTransaction? tx, string table, object dto, string identityColumn, CancellationToken ct = default);
    Task<int> UpdateAsync(SqlConnection conn, SqlTransaction? tx, string table, object setDto, object whereDto, CancellationToken ct = default);
    Task<int> DeleteAsync(SqlConnection conn, SqlTransaction? tx, string table, object whereDto, CancellationToken ct = default);
    Task<bool> ExistsAsync(SqlConnection conn, SqlTransaction? tx, string table, object whereDto, CancellationToken ct = default);
}

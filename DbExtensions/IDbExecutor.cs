using System.Data;
using Microsoft.Data.SqlClient;

public interface IDbExecutor
{
    Task<List<T>> QueryAsync<T>(
        string sql, object? param = null,
        int? timeoutSeconds = null, CommandType commandType = CommandType.Text,
        CancellationToken ct = default);

    Task<T?> QueryFirstOrDefaultAsync<T>(
        string sql, object? param = null,
        int? timeoutSeconds = null, CommandType commandType = CommandType.Text,
        CancellationToken ct = default);

    Task<int> ExecuteAsync(
        string sql, object? param = null,
        int? timeoutSeconds = null, CommandType commandType = CommandType.Text,
        CancellationToken ct = default);

    Task<T?> ExecuteScalarAsync<T>(
        string sql,
        object? param = null,
        int? timeoutSeconds = null,
        CommandType commandType = CommandType.Text,
        CancellationToken ct = default);
    
    Task<T?> QuerySingleOrDefaultAsync<T>(
        string sql, object? param = null,
        int? timeoutSeconds = null, CommandType commandType = CommandType.Text,
        CancellationToken ct = default);
    
    // 有交易
    Task<List<T>> QueryAsync<T>(SqlConnection conn, SqlTransaction? tx,
        string sql, object? param = null, int? timeoutSeconds = null,
        CommandType commandType = CommandType.Text, CancellationToken ct = default);

    Task<T?> QueryFirstOrDefaultAsync<T>(SqlConnection conn, SqlTransaction? tx,
        string sql, object? param = null, int? timeoutSeconds = null,
        CommandType commandType = CommandType.Text, CancellationToken ct = default);

    Task<T?> QuerySingleOrDefaultAsync<T>(SqlConnection conn, SqlTransaction? tx,
        string sql, object? param = null, int? timeoutSeconds = null,
        CommandType commandType = CommandType.Text, CancellationToken ct = default);

    Task<int> ExecuteAsync(SqlConnection conn, SqlTransaction? tx,
        string sql, object? param = null, int? timeoutSeconds = null,
        CommandType commandType = CommandType.Text, CancellationToken ct = default);

    Task<T?> ExecuteScalarAsync<T>(SqlConnection conn, SqlTransaction? tx,
        string sql, object? param = null, int? timeoutSeconds = null,
        CommandType commandType = CommandType.Text, CancellationToken ct = default);
    
    // 交易包裹
    Task TxAsync(
        Func<SqlConnection, SqlTransaction, CancellationToken, Task> work,
        IsolationLevel isolation = IsolationLevel.ReadCommitted,
        CancellationToken ct = default);

    Task<T> TxAsync<T>(
        Func<SqlConnection, SqlTransaction, CancellationToken, Task<T>> work,
        IsolationLevel isolation = IsolationLevel.ReadCommitted,
        CancellationToken ct = default);
}
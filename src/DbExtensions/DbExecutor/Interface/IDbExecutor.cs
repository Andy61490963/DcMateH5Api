using System.Data;
using Microsoft.Data.SqlClient;

namespace DbExtensions.DbExecutor.Interface;

public interface IDbExecutor
{
    SqlConnection Connection { get; }

    List<T> Query<T>(string sql, object? param = null, int? timeoutSeconds = null,
        CommandType commandType = CommandType.Text);

    T? QueryFirstOrDefault<T>(string sql, object? param = null, int? timeoutSeconds = null,
        CommandType commandType = CommandType.Text);

    T? QuerySingleOrDefault<T>(string sql, object? param = null, int? timeoutSeconds = null,
        CommandType commandType = CommandType.Text);

    int Execute(string sql, object? param = null, int? timeoutSeconds = null,
        CommandType commandType = CommandType.Text);

    T? ExecuteScalar<T>(string sql, object? param = null, int? timeoutSeconds = null,
        CommandType commandType = CommandType.Text);

    Task<List<T>> QueryAsync<T>(string sql, object? param = null, int? timeoutSeconds = null,
        CommandType commandType = CommandType.Text, CancellationToken ct = default);

    Task<T?> QueryFirstOrDefaultAsync<T>(string sql, object? param = null, int? timeoutSeconds = null,
        CommandType commandType = CommandType.Text, CancellationToken ct = default);

    Task<T?> QuerySingleOrDefaultAsync<T>(string sql, object? param = null, int? timeoutSeconds = null,
        CommandType commandType = CommandType.Text, CancellationToken ct = default);

    Task<int> ExecuteAsync(string sql, object? param = null, int? timeoutSeconds = null,
        CommandType commandType = CommandType.Text, CancellationToken ct = default);

    Task<T?> ExecuteScalarAsync<T>(string sql, object? param = null, int? timeoutSeconds = null,
        CommandType commandType = CommandType.Text, CancellationToken ct = default);

    // 交易包裹（維持既有能力）
    Task TxAsync(Func<SqlConnection, SqlTransaction, CancellationToken, Task> work,
        IsolationLevel isolation = IsolationLevel.ReadCommitted, CancellationToken ct = default);

    Task<T> TxAsync<T>(Func<SqlConnection, SqlTransaction, CancellationToken, Task<T>> work,
        IsolationLevel isolation = IsolationLevel.ReadCommitted, CancellationToken ct = default);

    int ExecuteInTx(SqlConnection conn, SqlTransaction tx, string sql, object? param = null,
        int? timeoutSeconds = null, CommandType commandType = CommandType.Text);

    T? ExecuteScalarInTx<T>(SqlConnection conn, SqlTransaction tx, string sql, object? param = null,
        int? timeoutSeconds = null, CommandType commandType = CommandType.Text);

    List<T> QueryInTx<T>(SqlConnection conn, SqlTransaction tx, string sql, object? param = null,
        int? timeoutSeconds = null, CommandType commandType = CommandType.Text);

    T? QueryFirstOrDefaultInTx<T>(SqlConnection conn, SqlTransaction tx, string sql, object? param = null,
        int? timeoutSeconds = null, CommandType commandType = CommandType.Text);

    T? QuerySingleOrDefaultInTx<T>(SqlConnection conn, SqlTransaction tx, string sql, object? param = null,
        int? timeoutSeconds = null, CommandType commandType = CommandType.Text);

    // 先保留（避免你 SQLGenerateHelper 大爆改），但之後建議淘汰
    Task<int> ExecuteInTxAsync(SqlConnection conn, SqlTransaction tx, string sql, object? param = null,
        int? timeoutSeconds = null, CommandType commandType = CommandType.Text, CancellationToken ct = default);

    Task<T?> ExecuteScalarInTxAsync<T>(SqlConnection conn, SqlTransaction tx, string sql, object? param = null,
        int? timeoutSeconds = null, CommandType commandType = CommandType.Text, CancellationToken ct = default);

    Task<List<T>> QueryInTxAsync<T>(SqlConnection conn, SqlTransaction tx, string sql, object? param = null,
        int? timeoutSeconds = null, CommandType commandType = CommandType.Text, CancellationToken ct = default);

    Task<T?> QueryFirstOrDefaultInTxAsync<T>(SqlConnection conn, SqlTransaction tx, string sql, object? param = null,
        int? timeoutSeconds = null, CommandType commandType = CommandType.Text, CancellationToken ct = default);

    Task<T?> QuerySingleOrDefaultInTxAsync<T>(SqlConnection conn, SqlTransaction tx, string sql, object? param = null,
        int? timeoutSeconds = null, CommandType commandType = CommandType.Text, CancellationToken ct = default);
}

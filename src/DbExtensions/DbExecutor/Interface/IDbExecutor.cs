using System.Data;
using Microsoft.Data.SqlClient;

namespace DbExtensions.DbExecutor.Interface;

public interface IDbExecutor
{
    /// <summary>
    /// 非交易查詢，回傳多筆資料。
    /// </summary>
    Task<List<T>> QueryAsync<T>(string sql, object? param = null, int? timeoutSeconds = null,
        CommandType commandType = CommandType.Text, CancellationToken ct = default);

    /// <summary>
    /// 非交易查詢，回傳首筆或 null。
    /// </summary>
    Task<T?> QueryFirstOrDefaultAsync<T>(string sql, object? param = null, int? timeoutSeconds = null,
        CommandType commandType = CommandType.Text, CancellationToken ct = default);

    /// <summary>
    /// 非交易執行新增/更新/刪除。
    /// </summary>
    Task<int> ExecuteAsync(string sql, object? param = null, int? timeoutSeconds = null,
        CommandType commandType = CommandType.Text, CancellationToken ct = default);

    /// <summary>
    /// 非交易執行純量查詢。
    /// </summary>
    Task<T?> ExecuteScalarAsync<T>(string sql, object? param = null, int? timeoutSeconds = null,
        CommandType commandType = CommandType.Text, CancellationToken ct = default);

    /// <summary>
    /// 交易包裹，所有交易內操作需共用同一個 conn/tx。
    /// </summary>
    Task TxAsync(Func<SqlConnection, SqlTransaction, CancellationToken, Task> work,
        IsolationLevel isolation = IsolationLevel.ReadCommitted, CancellationToken ct = default);

    /// <summary>
    /// 交易包裹（含回傳值），所有交易內操作需共用同一個 conn/tx。
    /// </summary>
    Task<T> TxAsync<T>(Func<SqlConnection, SqlTransaction, CancellationToken, Task<T>> work,
        IsolationLevel isolation = IsolationLevel.ReadCommitted, CancellationToken ct = default);

    /// <summary>
    /// 交易內執行新增/更新/刪除。
    /// </summary>
    Task<int> ExecuteInTxAsync(SqlConnection conn, SqlTransaction tx, string sql, object? param = null,
        int? timeoutSeconds = null, CommandType commandType = CommandType.Text, CancellationToken ct = default);

    /// <summary>
    /// 交易內執行純量查詢。
    /// </summary>
    Task<T?> ExecuteScalarInTxAsync<T>(SqlConnection conn, SqlTransaction tx, string sql, object? param = null,
        int? timeoutSeconds = null, CommandType commandType = CommandType.Text, CancellationToken ct = default);

    /// <summary>
    /// 交易內查詢多筆資料。
    /// </summary>
    Task<List<T>> QueryInTxAsync<T>(SqlConnection conn, SqlTransaction tx, string sql, object? param = null,
        int? timeoutSeconds = null, CommandType commandType = CommandType.Text, CancellationToken ct = default);

    /// <summary>
    /// 交易內查詢首筆或 null。
    /// </summary>
    Task<T?> QueryFirstOrDefaultInTxAsync<T>(SqlConnection conn, SqlTransaction tx, string sql, object? param = null,
        int? timeoutSeconds = null, CommandType commandType = CommandType.Text, CancellationToken ct = default);
}

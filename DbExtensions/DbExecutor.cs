using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;

namespace DynamicForm.DbExtensions;

public sealed class DbExecutor : IDbExecutor
{
    private readonly ISqlConnectionFactory _factory;
    private const int DefaultTimeoutSeconds = 30; 

    public DbExecutor(ISqlConnectionFactory factory) => _factory = factory;

    // -------------------------
    // 共用：建構 CommandDefinition
    // -------------------------
    // 最通用：用 IDbTransaction，未來要支援其他 DB 也 OK
    private static CommandDefinition BuildCmd(
        string sql,
        object? param,
        int? timeoutSeconds,
        CommandType commandType,
        IDbTransaction? tx,
        CancellationToken ct,
        CommandFlags flags = CommandFlags.Buffered)
    {
        return new CommandDefinition(
            commandText: sql,
            parameters: param,
            transaction: tx,                    // 把 tx 帶進去
            commandTimeout: timeoutSeconds ??　DefaultTimeoutSeconds,　// 過期時間
            commandType: commandType,
            flags: flags,
            cancellationToken: ct               // 把取消權杖向下傳
        );
    }

    // -------------------------
    // 無交易（短連線）版本：你原本的
    // -------------------------
    public async Task<List<T>> QueryAsync<T>(string sql, object? param = null,
        int? timeoutSeconds = null, CommandType commandType = CommandType.Text, CancellationToken ct = default)
    {
        await using var conn = _factory.Create();
        await conn.OpenAsync(ct);
        var cmd = BuildCmd(sql, param, timeoutSeconds, commandType, null, ct);
        var rows = await conn.QueryAsync<T>(cmd);
        return rows.AsList();
    }

    public async Task<T?> QueryFirstOrDefaultAsync<T>(string sql, object? param = null,
        int? timeoutSeconds = null, CommandType commandType = CommandType.Text, CancellationToken ct = default)
    {
        await using var conn = _factory.Create();
        await conn.OpenAsync(ct);
        var cmd = BuildCmd(sql, param, timeoutSeconds, commandType, null, ct);
        return await conn.QueryFirstOrDefaultAsync<T>(cmd);
    }

    public async Task<T?> QuerySingleOrDefaultAsync<T>(string sql, object? param = null,
        int? timeoutSeconds = null, CommandType commandType = CommandType.Text, CancellationToken ct = default)
    {
        await using var conn = _factory.Create();
        await conn.OpenAsync(ct);
        var cmd = BuildCmd(sql, param, timeoutSeconds, commandType, null, ct);
        return await conn.QuerySingleOrDefaultAsync<T>(cmd);
    }

    public async Task<int> ExecuteAsync(string sql, object? param = null,
        int? timeoutSeconds = null, CommandType commandType = CommandType.Text, CancellationToken ct = default)
    {
        await using var conn = _factory.Create();
        await conn.OpenAsync(ct);
        var cmd = BuildCmd(sql, param, timeoutSeconds, commandType, null, ct);
        return await conn.ExecuteAsync(cmd);
    }

    public async Task<T?> ExecuteScalarAsync<T>(string sql, object? param = null,
        int? timeoutSeconds = null, CommandType commandType = CommandType.Text, CancellationToken ct = default)
    {
        await using var conn = _factory.Create();
        await conn.OpenAsync(ct);
        var cmd = BuildCmd(sql, param, timeoutSeconds, commandType, null, ct);
        return await conn.ExecuteScalarAsync<T>(cmd);
    }

    // -------------------------
    // 有交易（使用既有 conn/tx）版本
    // 這些方法「不」開關連線，假設呼叫者已在 TxAsync 中開啟
    // -------------------------
    public async Task<List<T>> QueryAsync<T>(SqlConnection conn, SqlTransaction? tx,
        string sql, object? param = null, int? timeoutSeconds = null,
        CommandType commandType = CommandType.Text, CancellationToken ct = default)
    {
        var cmd = BuildCmd(sql, param, timeoutSeconds, commandType, tx, ct);
        var rows = await conn.QueryAsync<T>(cmd);
        return rows.AsList();
    }

    public Task<T?> QueryFirstOrDefaultAsync<T>(SqlConnection conn, SqlTransaction? tx,
        string sql, object? param = null, int? timeoutSeconds = null,
        CommandType commandType = CommandType.Text, CancellationToken ct = default)
    {
        var cmd = BuildCmd(sql, param, timeoutSeconds, commandType, tx, ct);
        return conn.QueryFirstOrDefaultAsync<T>(cmd);
    }

    public Task<T?> QuerySingleOrDefaultAsync<T>(SqlConnection conn, SqlTransaction? tx,
        string sql, object? param = null, int? timeoutSeconds = null,
        CommandType commandType = CommandType.Text, CancellationToken ct = default)
    {
        var cmd = BuildCmd(sql, param, timeoutSeconds, commandType, tx, ct);
        return conn.QuerySingleOrDefaultAsync<T>(cmd);
    }

    public Task<int> ExecuteAsync(SqlConnection conn, SqlTransaction? tx,
        string sql, object? param = null, int? timeoutSeconds = null,
        CommandType commandType = CommandType.Text, CancellationToken ct = default)
    {
        var cmd = BuildCmd(sql, param, timeoutSeconds, commandType, tx, ct);
        return conn.ExecuteAsync(cmd);
    }

    public Task<T?> ExecuteScalarAsync<T>(SqlConnection conn, SqlTransaction? tx,
        string sql, object? param = null, int? timeoutSeconds = null,
        CommandType commandType = CommandType.Text, CancellationToken ct = default)
    {
        var cmd = BuildCmd(sql, param, timeoutSeconds, commandType, tx, ct);
        return conn.ExecuteScalarAsync<T>(cmd);
    }

    // -------------------------
    // 交易包裹
    // 自己提供一個 Func<SqlConnection, SqlTransaction, CancellationToken, Task> 的委派
    // -------------------------
    public async Task TxAsync(
        Func<SqlConnection, SqlTransaction, CancellationToken, Task> work,
        IsolationLevel isolation = IsolationLevel.ReadCommitted, CancellationToken ct = default)
    {
        await using var conn = _factory.Create();
        await conn.OpenAsync(ct);
        await using var tx = (SqlTransaction)await conn.BeginTransactionAsync(isolation, ct);
        try
        {
            // 執行呼叫端傳進來的委派，並把 conn / tx / ct 傳給它
            await work(conn, tx, ct);
            await tx.CommitAsync(ct);
        }
        catch (OperationCanceledException)
        {
            await tx.RollbackAsync(CancellationToken.None);
            throw;
        }
        catch
        {
            await tx.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<T> TxAsync<T>(
        Func<SqlConnection, SqlTransaction, CancellationToken, Task<T>> work,
        IsolationLevel isolation = IsolationLevel.ReadCommitted, CancellationToken ct = default)
    {
        await using var conn = _factory.Create();
        await conn.OpenAsync(ct);
        await using var tx = (SqlTransaction)await conn.BeginTransactionAsync(isolation, ct);
        try
        {
            var result = await work(conn, tx, ct);
            await tx.CommitAsync(ct);
            return result;
        }
        catch (OperationCanceledException)
        {
            await tx.RollbackAsync(CancellationToken.None);
            throw;
        }
        catch
        {
            await tx.RollbackAsync(CancellationToken.None);
            throw;
        }
    }
}

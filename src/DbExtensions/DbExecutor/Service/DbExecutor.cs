using System.Data;
using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using Dapper;
using DbExtensions.DbExecutor.Interface;
using DcMateH5.Abstractions.Log;
using DcMateH5.Abstractions.Log.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;

namespace DbExtensions.DbExecutor.Service;

public sealed class DbExecutor : IDbExecutor
{
    private const int DefaultTimeoutSeconds = 30;
    private const int MaxParameterLength = 4000;

    private static class HttpItemKeys
    {
        public const string CorrelationId = "CorrelationId";
    }

    private readonly SqlConnection _connection;              // ✅ DI Scoped connection
    private readonly DbTransactionContext _txContext;        // ✅ Scoped ambient tx holder
    private readonly ILogService _logService;
    private readonly IHttpContextAccessor _http;

    public DbExecutor(
        SqlConnection connection,
        DbTransactionContext txContext,
        ILogService logService,
        IHttpContextAccessor http)
    {
        _connection = connection;
        _txContext = txContext;
        _logService = logService;
        _http = http;
    }

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
            transaction: tx,
            commandTimeout: timeoutSeconds ?? DefaultTimeoutSeconds,
            commandType: commandType,
            flags: flags,
            cancellationToken: ct);
    }

    public static Guid GetCorrelationId(HttpContext? context)
    {
        if (context == null)
        {
            return Guid.NewGuid();
        }

        if (!context.Items.TryGetValue(HttpItemKeys.CorrelationId, out var value) || value is not Guid guid)
        {
            guid = Guid.NewGuid();
            context.Items[HttpItemKeys.CorrelationId] = guid;
        }

        return guid;
    }

    private SqlLogEntry CreateLogEntry(string sql, object? param)
    {
        return new SqlLogEntry
        {
            RequestId = GetCorrelationId(_http.HttpContext),
            ExecutedAt = DateTime.Now,
            SqlText = sql,
            Parameters = SerializeParameters(param),
            UserId = GetUserId(),
            IpAddress = GetIpAddress()
        };
    }

    private Guid? GetUserId()
    {
        var user = _http.HttpContext?.User;
        var id = user?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                 ?? user?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (Guid.TryParse(id, out var userId))
        {
            return userId;
        }

        return null;
    }

    private string? GetIpAddress()
    {
        return _http.HttpContext?.Connection?.RemoteIpAddress?.ToString();
    }

    private static string? SerializeParameters(object? param)
    {
        if (param == null)
        {
            return null;
        }

        object toSerialize = param;

        if (param is DynamicParameters dp)
        {
            var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var name in dp.ParameterNames)
            {
                dict[name] = dp.Get<object?>(name);
            }

            toSerialize = dict;
        }

        try
        {
            var json = JsonSerializer.Serialize(toSerialize);
            if (json.Length > MaxParameterLength)
            {
                json = json[..MaxParameterLength];
            }

            return json;
        }
        catch
        {
            return null;
        }
    }

    private async Task TryLogAsync(SqlLogEntry entry)
    {
        try
        {
            await _logService.LogAsync(entry, CancellationToken.None);
        }
        catch
        {
            // best-effort
        }
    }

    private async Task EnsureOpenAsync(CancellationToken ct)
    {
        if (_connection.State == ConnectionState.Open)
        {
            return;
        }

        await _connection.OpenAsync(ct);
    }

    private async Task<TResult> ExecuteWithLogAsync<TResult>(
        string sql,
        object? param,
        Func<Task<TResult>> action,
        Func<TResult, int> affectedRows,
        CancellationToken ct)
    {
        var log = CreateLogEntry(sql, param);
        var sw = Stopwatch.StartNew();

        try
        {
            var result = await action();
            log.AffectedRows = affectedRows(result);
            log.IsSuccess = true;
            return result;
        }
        catch (Exception ex)
        {
            log.IsSuccess = false;
            log.ErrorMessage = ex.Message;
            throw;
        }
        finally
        {
            sw.Stop();
            log.DurationMs = sw.ElapsedMilliseconds;
            await TryLogAsync(log);
        }
    }

    // ✅ 無交易 API：自動吃 ambient tx（如果有 TxAsync 包著）
    public Task<List<T>> QueryAsync<T>(string sql, object? param = null, int? timeoutSeconds = null,
        CommandType commandType = CommandType.Text, CancellationToken ct = default)
    {
        return ExecuteWithLogAsync(
            sql,
            param,
            async () =>
            {
                await EnsureOpenAsync(ct);
                var cmd = BuildCmd(sql, param, timeoutSeconds, commandType, _txContext.Current, ct);
                var rows = await _connection.QueryAsync<T>(cmd);
                return rows.AsList();
            },
            r => r.Count,
            ct);
    }

    public Task<T?> QueryFirstOrDefaultAsync<T>(string sql, object? param = null, int? timeoutSeconds = null,
        CommandType commandType = CommandType.Text, CancellationToken ct = default)
    {
        return ExecuteWithLogAsync(
            sql,
            param,
            async () =>
            {
                await EnsureOpenAsync(ct);
                var cmd = BuildCmd(sql, param, timeoutSeconds, commandType, _txContext.Current, ct);
                return await _connection.QueryFirstOrDefaultAsync<T>(cmd);
            },
            r => r == null ? 0 : 1,
            ct);
    }

    public Task<int> ExecuteAsync(string sql, object? param = null, int? timeoutSeconds = null,
        CommandType commandType = CommandType.Text, CancellationToken ct = default)
    {
        return ExecuteWithLogAsync(
            sql,
            param,
            async () =>
            {
                await EnsureOpenAsync(ct);
                var cmd = BuildCmd(sql, param, timeoutSeconds, commandType, _txContext.Current, ct);
                return await _connection.ExecuteAsync(cmd);
            },
            r => r,
            ct);
    }

    public Task<T?> ExecuteScalarAsync<T>(string sql, object? param = null, int? timeoutSeconds = null,
        CommandType commandType = CommandType.Text, CancellationToken ct = default)
    {
        return ExecuteWithLogAsync(
            sql,
            param,
            async () =>
            {
                await EnsureOpenAsync(ct);
                var cmd = BuildCmd(sql, param, timeoutSeconds, commandType, _txContext.Current, ct);
                return await _connection.ExecuteScalarAsync<T>(cmd);
            },
            r => r == null ? 0 : 1,
            ct);
    }

    // ✅ 交易包裹：把 tx 設進 context，讓 QueryAsync/ExecuteAsync 自動加入交易
    public async Task TxAsync(Func<SqlConnection, SqlTransaction, CancellationToken, Task> work,
        IsolationLevel isolation = IsolationLevel.ReadCommitted, CancellationToken ct = default)
    {
        await EnsureOpenAsync(ct);

        if (_txContext.HasTransaction)
        {
            throw new InvalidOperationException("不支援巢狀交易：目前 Scope 已存在 Transaction。");
        }

        await using var tx = (SqlTransaction)await _connection.BeginTransactionAsync(isolation, ct);
        _txContext.Set(tx);

        try
        {
            await work(_connection, tx, ct);
            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(CancellationToken.None);
            throw;
        }
        finally
        {
            _txContext.Clear();
        }
    }

    public async Task<T> TxAsync<T>(Func<SqlConnection, SqlTransaction, CancellationToken, Task<T>> work,
        IsolationLevel isolation = IsolationLevel.ReadCommitted, CancellationToken ct = default)
    {
        await EnsureOpenAsync(ct);

        if (_txContext.HasTransaction)
        {
            throw new InvalidOperationException("不支援巢狀交易：目前 Scope 已存在 Transaction。");
        }

        await using var tx = (SqlTransaction)await _connection.BeginTransactionAsync(isolation, ct);
        _txContext.Set(tx);

        try
        {
            var result = await work(_connection, tx, ct);
            await tx.CommitAsync(ct);
            return result;
        }
        catch
        {
            await tx.RollbackAsync(CancellationToken.None);
            throw;
        }
        finally
        {
            _txContext.Clear();
        }
    }

    // ✅ 舊 InTx API 保留（不逼你馬上改 SQLGenerateHelper）
    public Task<int> ExecuteInTxAsync(SqlConnection conn, SqlTransaction tx, string sql, object? param = null,
        int? timeoutSeconds = null, CommandType commandType = CommandType.Text, CancellationToken ct = default)
    {
        return ExecuteWithLogAsync(
            sql,
            param,
            async () =>
            {
                var cmd = BuildCmd(sql, param, timeoutSeconds, commandType, tx, ct);
                return await conn.ExecuteAsync(cmd);
            },
            r => r,
            ct);
    }

    public Task<T?> ExecuteScalarInTxAsync<T>(SqlConnection conn, SqlTransaction tx, string sql, object? param = null,
        int? timeoutSeconds = null, CommandType commandType = CommandType.Text, CancellationToken ct = default)
    {
        return ExecuteWithLogAsync(
            sql,
            param,
            async () =>
            {
                var cmd = BuildCmd(sql, param, timeoutSeconds, commandType, tx, ct);
                return await conn.ExecuteScalarAsync<T>(cmd);
            },
            r => r == null ? 0 : 1,
            ct);
    }

    public Task<List<T>> QueryInTxAsync<T>(SqlConnection conn, SqlTransaction tx, string sql, object? param = null,
        int? timeoutSeconds = null, CommandType commandType = CommandType.Text, CancellationToken ct = default)
    {
        return ExecuteWithLogAsync(
            sql,
            param,
            async () =>
            {
                var cmd = BuildCmd(sql, param, timeoutSeconds, commandType, tx, ct);
                var rows = await conn.QueryAsync<T>(cmd);
                return rows.AsList();
            },
            r => r.Count,
            ct);
    }

    public Task<T?> QueryFirstOrDefaultInTxAsync<T>(SqlConnection conn, SqlTransaction tx, string sql, object? param = null,
        int? timeoutSeconds = null, CommandType commandType = CommandType.Text, CancellationToken ct = default)
    {
        return ExecuteWithLogAsync(
            sql,
            param,
            async () =>
            {
                var cmd = BuildCmd(sql, param, timeoutSeconds, commandType, tx, ct);
                return await conn.QueryFirstOrDefaultAsync<T>(cmd);
            },
            r => r == null ? 0 : 1,
            ct);
    }

}

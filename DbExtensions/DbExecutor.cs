using System.Data;
using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using Dapper;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using DcMateH5Api.Logging;

namespace DcMateH5Api.DbExtensions;

public sealed class DbExecutor : IDbExecutor
{
    private readonly ISqlConnectionFactory _factory;
    private readonly ISqlLogService _logService;
    private readonly IHttpContextAccessor _http;
    private const int DefaultTimeoutSeconds = 30;
    private const int MaxParameterLength = 4000;

    public DbExecutor(ISqlConnectionFactory factory, ISqlLogService logService, IHttpContextAccessor http)
    {
        _factory = factory;
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

    // helper methods for logging
    private SqlLogEntry CreateLogEntry(string sql, object? param)
    {
        return new SqlLogEntry
        {
            ExecutedAt = DateTime.UtcNow,
            SqlText = sql,
            Parameters = SerializeParameters(param),
            UserId = GetUserId(),
            IpAddress = GetIpAddress()
        };
    }

    private string? GetUserId()
    {
        var user = _http.HttpContext?.User;
        return user?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? user?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }

    private string? GetIpAddress()
        => _http.HttpContext?.Connection?.RemoteIpAddress?.ToString();

    private static string? SerializeParameters(object? param)
    {
        if (param == null) return null;

        object toSerialize = param;

        if (param is DynamicParameters dp)
        {
            var dict = new Dictionary<string, object?>();
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
                json = json[..MaxParameterLength];
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
            // swallow logging failures
        }
    }

    // -------------------------
    // 無交易（短連線）版本
    // -------------------------
    public async Task<List<T>> QueryAsync<T>(string sql, object? param = null,
        int? timeoutSeconds = null, CommandType commandType = CommandType.Text, CancellationToken ct = default)
    {
        var log = CreateLogEntry(sql, param);
        var sw = Stopwatch.StartNew();
        List<T> result = new();
        try
        {
            await using var conn = _factory.Create();
            await conn.OpenAsync(ct);
            var cmd = BuildCmd(sql, param, timeoutSeconds, commandType, null, ct);
            var rows = await conn.QueryAsync<T>(cmd);
            result = rows.AsList();
            log.AffectedRows = result.Count;
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

    public async Task<T?> QueryFirstOrDefaultAsync<T>(string sql, object? param = null,
        int? timeoutSeconds = null, CommandType commandType = CommandType.Text, CancellationToken ct = default)
    {
        var log = CreateLogEntry(sql, param);
        var sw = Stopwatch.StartNew();
        try
        {
            await using var conn = _factory.Create();
            await conn.OpenAsync(ct);
            var cmd = BuildCmd(sql, param, timeoutSeconds, commandType, null, ct);
            var item = await conn.QueryFirstOrDefaultAsync<T>(cmd);
            log.AffectedRows = item != null ? 1 : 0;
            log.IsSuccess = true;
            return item;
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

    public async Task<T?> QuerySingleOrDefaultAsync<T>(string sql, object? param = null,
        int? timeoutSeconds = null, CommandType commandType = CommandType.Text, CancellationToken ct = default)
    {
        var log = CreateLogEntry(sql, param);
        var sw = Stopwatch.StartNew();
        try
        {
            await using var conn = _factory.Create();
            await conn.OpenAsync(ct);
            var cmd = BuildCmd(sql, param, timeoutSeconds, commandType, null, ct);
            var item = await conn.QuerySingleOrDefaultAsync<T>(cmd);
            log.AffectedRows = item != null ? 1 : 0;
            log.IsSuccess = true;
            return item;
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

    public async Task<int> ExecuteAsync(string sql, object? param = null,
        int? timeoutSeconds = null, CommandType commandType = CommandType.Text, CancellationToken ct = default)
    {
        var log = CreateLogEntry(sql, param);
        var sw = Stopwatch.StartNew();
        try
        {
            await using var conn = _factory.Create();
            await conn.OpenAsync(ct);
            var cmd = BuildCmd(sql, param, timeoutSeconds, commandType, null, ct);
            var affected = await conn.ExecuteAsync(cmd);
            log.AffectedRows = affected;
            log.IsSuccess = true;
            return affected;
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

    public async Task<T?> ExecuteScalarAsync<T>(string sql, object? param = null,
        int? timeoutSeconds = null, CommandType commandType = CommandType.Text, CancellationToken ct = default)
    {
        var log = CreateLogEntry(sql, param);
        var sw = Stopwatch.StartNew();
        try
        {
            await using var conn = _factory.Create();
            await conn.OpenAsync(ct);
            var cmd = BuildCmd(sql, param, timeoutSeconds, commandType, null, ct);
            var value = await conn.ExecuteScalarAsync<T>(cmd);
            log.AffectedRows = value != null ? 1 : 0;
            log.IsSuccess = true;
            return value;
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

    // -------------------------
    // 有交易（使用既有 conn/tx）版本
    // -------------------------
    public async Task<List<T>> QueryAsync<T>(SqlConnection conn, SqlTransaction? tx,
        string sql, object? param = null, int? timeoutSeconds = null,
        CommandType commandType = CommandType.Text, CancellationToken ct = default)
    {
        var log = CreateLogEntry(sql, param);
        var sw = Stopwatch.StartNew();
        List<T> result = new();
        try
        {
            var cmd = BuildCmd(sql, param, timeoutSeconds, commandType, tx, ct);
            var rows = await conn.QueryAsync<T>(cmd);
            result = rows.AsList();
            log.AffectedRows = result.Count;
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

    public async Task<T?> QueryFirstOrDefaultAsync<T>(SqlConnection conn, SqlTransaction? tx,
        string sql, object? param = null, int? timeoutSeconds = null,
        CommandType commandType = CommandType.Text, CancellationToken ct = default)
    {
        var log = CreateLogEntry(sql, param);
        var sw = Stopwatch.StartNew();
        try
        {
            var cmd = BuildCmd(sql, param, timeoutSeconds, commandType, tx, ct);
            var item = await conn.QueryFirstOrDefaultAsync<T>(cmd);
            log.AffectedRows = item != null ? 1 : 0;
            log.IsSuccess = true;
            return item;
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

    public async Task<T?> QuerySingleOrDefaultAsync<T>(SqlConnection conn, SqlTransaction? tx,
        string sql, object? param = null, int? timeoutSeconds = null,
        CommandType commandType = CommandType.Text, CancellationToken ct = default)
    {
        var log = CreateLogEntry(sql, param);
        var sw = Stopwatch.StartNew();
        try
        {
            var cmd = BuildCmd(sql, param, timeoutSeconds, commandType, tx, ct);
            var item = await conn.QuerySingleOrDefaultAsync<T>(cmd);
            log.AffectedRows = item != null ? 1 : 0;
            log.IsSuccess = true;
            return item;
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

    public async Task<int> ExecuteAsync(SqlConnection conn, SqlTransaction? tx,
        string sql, object? param = null, int? timeoutSeconds = null,
        CommandType commandType = CommandType.Text, CancellationToken ct = default)
    {
        var log = CreateLogEntry(sql, param);
        var sw = Stopwatch.StartNew();
        try
        {
            var cmd = BuildCmd(sql, param, timeoutSeconds, commandType, tx, ct);
            var affected = await conn.ExecuteAsync(cmd);
            log.AffectedRows = affected;
            log.IsSuccess = true;
            return affected;
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

    public async Task<T?> ExecuteScalarAsync<T>(SqlConnection conn, SqlTransaction? tx,
        string sql, object? param = null, int? timeoutSeconds = null,
        CommandType commandType = CommandType.Text, CancellationToken ct = default)
    {
        var log = CreateLogEntry(sql, param);
        var sw = Stopwatch.StartNew();
        try
        {
            var cmd = BuildCmd(sql, param, timeoutSeconds, commandType, tx, ct);
            var value = await conn.ExecuteScalarAsync<T>(cmd);
            log.AffectedRows = value != null ? 1 : 0;
            log.IsSuccess = true;
            return value;
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

    // -------------------------
    // 交易包裹
    // -------------------------
    public async Task TxAsync(
        Func<SqlConnection, SqlTransaction, CancellationToken, Task> work,
        IsolationLevel isolation = IsolationLevel.ReadCommitted, CancellationToken ct = default)
    {
        var log = CreateLogEntry("TxAsync", null);
        var sw = Stopwatch.StartNew();
        try
        {
            await using var conn = _factory.Create();
            await conn.OpenAsync(ct);
            await using var tx = (SqlTransaction)await conn.BeginTransactionAsync(isolation, ct);
            try
            {
                await work(conn, tx, ct);
                await tx.CommitAsync(ct);
                log.IsSuccess = true;
            }
            catch
            {
                await tx.RollbackAsync(CancellationToken.None);
                log.IsSuccess = false;
                throw;
            }
        }
        catch (Exception ex)
        {
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

    public async Task<T> TxAsync<T>(
        Func<SqlConnection, SqlTransaction, CancellationToken, Task<T>> work,
        IsolationLevel isolation = IsolationLevel.ReadCommitted, CancellationToken ct = default)
    {
        var log = CreateLogEntry("TxAsync<T>", null);
        var sw = Stopwatch.StartNew();
        try
        {
            await using var conn = _factory.Create();
            await conn.OpenAsync(ct);
            await using var tx = (SqlTransaction)await conn.BeginTransactionAsync(isolation, ct);
            try
            {
                var result = await work(conn, tx, ct);
                await tx.CommitAsync(ct);
                log.IsSuccess = true;
                return result;
            }
            catch
            {
                await tx.RollbackAsync(CancellationToken.None);
                log.IsSuccess = false;
                throw;
            }
        }
        catch (Exception ex)
        {
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
}

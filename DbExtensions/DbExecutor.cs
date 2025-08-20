using System.Data;
using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using Azure;
using Dapper;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using DcMateH5Api.Logging;

namespace DcMateH5Api.DbExtensions;

/// <summary>
/// DbExecutor 是一個包裝 Dapper 的資料存取層，
/// 對外提供 Query/Execute/Transaction 等方法，
/// 並且內建 SQL 執行紀錄 (Logging)。
/// </summary>
public sealed class DbExecutor : IDbExecutor
{
    private readonly ISqlConnectionFactory _factory;
    private readonly ISqlLogService _logService;
    private readonly IHttpContextAccessor _http;
    private const int DefaultTimeoutSeconds = 30; // 預設逾時秒數
    private const int MaxParameterLength = 4000; // 參數序列化上限 (避免 DB 過長塞爆)
    
    private const string CorrelationIdKey = "CorrelationId";
    public DbExecutor(ISqlConnectionFactory factory, ISqlLogService logService, IHttpContextAccessor http)
    {
        _factory = factory;
        _logService = logService;
        _http = http;
    }

    /// <summary>
    /// 建立 Dapper CommandDefinition 物件
    /// </summary>
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
    
    /// <summary>
    /// 讓同一個 Request 的所有 SQL Log 共用同一個 CorrelationId
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public static Guid GetCorrelationId(HttpContext? context)
    {
        // 如果沒有 HttpContext（例如非 Web 環境），就直接給一個新的 Guid。
        if (context == null) return Guid.NewGuid();

        // 檢查 HttpContext.Items（每個 Request 自帶的字典），
        // 看看有沒有存過 "CorrelationId"
        if (!context.Items.TryGetValue(CorrelationIdKey, out var value) || value is not Guid guid)
        {
            // 沒有的話就生成一個新的 Guid
            guid = Guid.NewGuid();
            context.Items[CorrelationIdKey] = guid;
        }

        return guid;
    }
    
    /// <summary>
    /// 建立 SQL 紀錄物件，包含 SQL/參數/使用者資訊。
    /// </summary>
    private SqlLogEntry CreateLogEntry(string sql, object? param)
    {
        return new SqlLogEntry
        {
            RequestId =  GetCorrelationId(_http.HttpContext),
            ExecutedAt = DateTime.UtcNow,
            SqlText = sql,
            Parameters = SerializeParameters(param),
            UserId = GetUserId(),
            IpAddress = GetIpAddress()
        };
    }

    /// <summary>
    /// 從 Claims 取得 UserId（JWT "sub"）
    /// </summary>
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
    
    /// <summary>
    /// 從 HttpContext 取得來源 IP
    /// </summary>
    private string? GetIpAddress()
        => _http.HttpContext?.Connection?.RemoteIpAddress?.ToString();

    /// <summary>
    /// 將 SQL 參數序列化成 JSON 
    /// </summary>
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
                json = json[..MaxParameterLength]; // 過長截斷
            return json;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 嘗試寫入 SQL Log 
    /// </summary>
    private async Task TryLogAsync(SqlLogEntry entry)
    {
        try
        {
            await _logService.LogAsync(entry, CancellationToken.None);
        }
        catch (Exception ex)
        {
            // 失敗不處理，會影響主交易
            throw new InvalidOperationException("SQL Log 寫入失敗", ex);
        }
    }

    // -------------------------
    // 無交易（短連線）版本
    // -------------------------
    
    /// <summary>
    /// 查詢多筆資料
    /// </summary>
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

    /// <summary>
    /// 查詢第一筆資料，找不到就回傳 null
    /// </summary>
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

    /// <summary>
    /// 查詢唯一一筆資料，找不到回傳 null
    /// </summary>
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

    /// <summary>
    /// 執行 INSERT / UPDATE / DELETE，回傳受影響筆數
    /// </summary>
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

    /// <summary>
    /// 執行查詢，回傳單一值 (scalar)
    /// </summary>
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
    // 有交易版本 (使用既有 conn/tx)
    // -------------------------
    // （以下跟上面幾乎一樣，只是多帶 SqlConnection / SqlTransaction）
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
    // 交易包裹 (Transaction Scope)
    // -------------------------
    
    /// <summary>
    /// 包一個交易，不回傳值
    /// </summary>
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
    
    /// <summary>
    /// 包一個交易，並且回傳泛型結果
    /// </summary>
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

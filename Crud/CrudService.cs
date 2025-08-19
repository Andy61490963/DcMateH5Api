using System.Data;
using System.Diagnostics;
using Dapper;
using Microsoft.Data.SqlClient;
using DcMateH5Api.Logging;

/// <summary>
/// Default implementation of <see cref="ICrudService"/> which delegates SQL generation to
/// <see cref="ICrudSqlBuilder"/> and execution to <see cref="IDbExecutor"/>.
/// </summary>
public class CrudService : ICrudService
{
    private readonly ICrudSqlBuilder _builder;
    private readonly IDbExecutor _db;
    private readonly ISqlLogService _logService;

    public CrudService(ICrudSqlBuilder builder, IDbExecutor db, ISqlLogService logService)
    {
        _builder = builder;
        _db = db;
        _logService = logService;
    }

    public async Task<int> InsertAsync(string table, object dto, CancellationToken ct = default)
    {
        var (sql, param) = _builder.BuildInsert(table, dto);
        var executedAt = DateTime.UtcNow;
        var sw = Stopwatch.StartNew();
        var affected = 0;
        string? error = null;
        try
        {
            affected = await _db.ExecuteAsync(sql, param, ct: ct);
            return affected;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            throw new InvalidOperationException("Failed to execute INSERT.", ex);
        }
        finally
        {
            sw.Stop();
            await _logService.LogAsync(new SqlLogEntry
            {
                SqlText = sql,
                Parameters = SqlLogService.SerializeParams(param),
                ExecutedAt = executedAt,
                DurationMs = sw.ElapsedMilliseconds,
                AffectedRows = affected,
                ErrorMessage = error,
                IsSuccess = error is null
            }, ct);
        }
    }

    public async Task<T?> InsertOutputAsync<T>(string table, object dto, string identityColumn, CancellationToken ct = default)
    {
        var (sql, param) = _builder.BuildInsertOutput(table, dto, identityColumn);
        var executedAt = DateTime.UtcNow;
        var sw = Stopwatch.StartNew();
        T? result = default;
        string? error = null;
        try
        {
            result = await _db.ExecuteScalarAsync<T>(sql, param, ct: ct);
            return result;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            throw new InvalidOperationException("Failed to execute INSERT with OUTPUT.", ex);
        }
        finally
        {
            sw.Stop();
            await _logService.LogAsync(new SqlLogEntry
            {
                SqlText = sql,
                Parameters = SqlLogService.SerializeParams(param),
                ExecutedAt = executedAt,
                DurationMs = sw.ElapsedMilliseconds,
                AffectedRows = result != null ? 1 : 0,
                ErrorMessage = error,
                IsSuccess = error is null
            }, ct);
        }
    }

    public async Task<int> UpdateAsync(string table, object setDto, object whereDto, CancellationToken ct = default)
    {
        var (sql, param) = _builder.BuildUpdate(table, setDto, whereDto);
        var executedAt = DateTime.UtcNow;
        var sw = Stopwatch.StartNew();
        var affected = 0;
        string? error = null;
        try
        {
            affected = await _db.ExecuteAsync(sql, param, ct: ct);
            return affected;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            throw new InvalidOperationException("Failed to execute UPDATE.", ex);
        }
        finally
        {
            sw.Stop();
            await _logService.LogAsync(new SqlLogEntry
            {
                SqlText = sql,
                Parameters = SqlLogService.SerializeParams(param),
                ExecutedAt = executedAt,
                DurationMs = sw.ElapsedMilliseconds,
                AffectedRows = affected,
                ErrorMessage = error,
                IsSuccess = error is null
            }, ct);
        }
    }

    public async Task<int> DeleteAsync(string table, object whereDto, CancellationToken ct = default)
    {
        var (sql, param) = _builder.BuildDelete(table, whereDto);
        var executedAt = DateTime.UtcNow;
        var sw = Stopwatch.StartNew();
        var affected = 0;
        string? error = null;
        try
        {
            affected = await _db.ExecuteAsync(sql, param, ct: ct);
            return affected;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            throw new InvalidOperationException("Failed to execute DELETE.", ex);
        }
        finally
        {
            sw.Stop();
            await _logService.LogAsync(new SqlLogEntry
            {
                SqlText = sql,
                Parameters = SqlLogService.SerializeParams(param),
                ExecutedAt = executedAt,
                DurationMs = sw.ElapsedMilliseconds,
                AffectedRows = affected,
                ErrorMessage = error,
                IsSuccess = error is null
            }, ct);
        }
    }

    public async Task<bool> ExistsAsync(string table, object whereDto, CancellationToken ct = default)
    {
        var (sql, param) = _builder.BuildExists(table, whereDto);
        var executedAt = DateTime.UtcNow;
        var sw = Stopwatch.StartNew();
        int? result = null;
        string? error = null;
        try
        {
            result = await _db.ExecuteScalarAsync<int?>(sql, param, ct: ct);
            return result.HasValue;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            throw new InvalidOperationException("Failed to execute EXISTS.", ex);
        }
        finally
        {
            sw.Stop();
            await _logService.LogAsync(new SqlLogEntry
            {
                SqlText = sql,
                Parameters = SqlLogService.SerializeParams(param),
                ExecutedAt = executedAt,
                DurationMs = sw.ElapsedMilliseconds,
                AffectedRows = result.HasValue ? 1 : 0,
                ErrorMessage = error,
                IsSuccess = error is null
            }, ct);
        }
    }

    public async Task<int> InsertAsync(SqlConnection conn, SqlTransaction? tx, string table, object dto, CancellationToken ct = default)
    {
        var (sql, param) = _builder.BuildInsert(table, dto);
        var executedAt = DateTime.UtcNow;
        var sw = Stopwatch.StartNew();
        var affected = 0;
        string? error = null;
        try
        {
            affected = await _db.ExecuteAsync(conn, tx, sql, param, ct: ct);
            return affected;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            throw new InvalidOperationException("Failed to execute INSERT.", ex);
        }
        finally
        {
            sw.Stop();
            await _logService.LogAsync(new SqlLogEntry
            {
                SqlText = sql,
                Parameters = SqlLogService.SerializeParams(param),
                ExecutedAt = executedAt,
                DurationMs = sw.ElapsedMilliseconds,
                AffectedRows = affected,
                ErrorMessage = error,
                IsSuccess = error is null
            }, ct);
        }
    }

    public async Task<T?> InsertOutputAsync<T>(SqlConnection conn, SqlTransaction? tx, string table, object dto, string identityColumn, CancellationToken ct = default)
    {
        var (sql, param) = _builder.BuildInsertOutput(table, dto, identityColumn);
        var executedAt = DateTime.UtcNow;
        var sw = Stopwatch.StartNew();
        T? result = default;
        string? error = null;
        try
        {
            result = await _db.ExecuteScalarAsync<T>(conn, tx, sql, param, ct: ct);
            return result;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            throw new InvalidOperationException("Failed to execute INSERT with OUTPUT.", ex);
        }
        finally
        {
            sw.Stop();
            await _logService.LogAsync(new SqlLogEntry
            {
                SqlText = sql,
                Parameters = SqlLogService.SerializeParams(param),
                ExecutedAt = executedAt,
                DurationMs = sw.ElapsedMilliseconds,
                AffectedRows = result != null ? 1 : 0,
                ErrorMessage = error,
                IsSuccess = error is null
            }, ct);
        }
    }

    public async Task<int> UpdateAsync(SqlConnection conn, SqlTransaction? tx, string table, object setDto, object whereDto, CancellationToken ct = default)
    {
        var (sql, param) = _builder.BuildUpdate(table, setDto, whereDto);
        var executedAt = DateTime.UtcNow;
        var sw = Stopwatch.StartNew();
        var affected = 0;
        string? error = null;
        try
        {
            affected = await _db.ExecuteAsync(conn, tx, sql, param, ct: ct);
            return affected;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            throw new InvalidOperationException("Failed to execute UPDATE.", ex);
        }
        finally
        {
            sw.Stop();
            await _logService.LogAsync(new SqlLogEntry
            {
                SqlText = sql,
                Parameters = SqlLogService.SerializeParams(param),
                ExecutedAt = executedAt,
                DurationMs = sw.ElapsedMilliseconds,
                AffectedRows = affected,
                ErrorMessage = error,
                IsSuccess = error is null
            }, ct);
        }
    }

    public async Task<int> DeleteAsync(SqlConnection conn, SqlTransaction? tx, string table, object whereDto, CancellationToken ct = default)
    {
        var (sql, param) = _builder.BuildDelete(table, whereDto);
        var executedAt = DateTime.UtcNow;
        var sw = Stopwatch.StartNew();
        var affected = 0;
        string? error = null;
        try
        {
            affected = await _db.ExecuteAsync(conn, tx, sql, param, ct: ct);
            return affected;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            throw new InvalidOperationException("Failed to execute DELETE.", ex);
        }
        finally
        {
            sw.Stop();
            await _logService.LogAsync(new SqlLogEntry
            {
                SqlText = sql,
                Parameters = SqlLogService.SerializeParams(param),
                ExecutedAt = executedAt,
                DurationMs = sw.ElapsedMilliseconds,
                AffectedRows = affected,
                ErrorMessage = error,
                IsSuccess = error is null
            }, ct);
        }
    }

    public async Task<bool> ExistsAsync(SqlConnection conn, SqlTransaction? tx, string table, object whereDto, CancellationToken ct = default)
    {
        var (sql, param) = _builder.BuildExists(table, whereDto);
        var executedAt = DateTime.UtcNow;
        var sw = Stopwatch.StartNew();
        int? result = null;
        string? error = null;
        try
        {
            result = await _db.ExecuteScalarAsync<int?>(conn, tx, sql, param, ct: ct);
            return result.HasValue;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            throw new InvalidOperationException("Failed to execute EXISTS.", ex);
        }
        finally
        {
            sw.Stop();
            await _logService.LogAsync(new SqlLogEntry
            {
                SqlText = sql,
                Parameters = SqlLogService.SerializeParams(param),
                ExecutedAt = executedAt,
                DurationMs = sw.ElapsedMilliseconds,
                AffectedRows = result.HasValue ? 1 : 0,
                ErrorMessage = error,
                IsSuccess = error is null
            }, ct);
        }
    }
}

using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;

/// <summary>
/// Default implementation of <see cref="ICrudService"/> which delegates SQL generation to
/// <see cref="ICrudSqlBuilder"/> and execution to <see cref="IDbExecutor"/>.
/// </summary>
public class CrudService : ICrudService
{
    private readonly ICrudSqlBuilder _builder;
    private readonly IDbExecutor _db;

    public CrudService(ICrudSqlBuilder builder, IDbExecutor db)
    {
        _builder = builder;
        _db = db;
    }

    public async Task<int> InsertAsync(string table, object dto, CancellationToken ct = default)
    {
        var (sql, param) = _builder.BuildInsert(table, dto);
        try
        {
            return await _db.ExecuteAsync(sql, param, ct: ct);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to execute INSERT.", ex);
        }
    }

    public async Task<T?> InsertOutputAsync<T>(string table, object dto, string identityColumn, CancellationToken ct = default)
    {
        var (sql, param) = _builder.BuildInsertOutput(table, dto, identityColumn);
        try
        {
            return await _db.ExecuteScalarAsync<T>(sql, param, ct: ct);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to execute INSERT with OUTPUT.", ex);
        }
    }

    public async Task<int> UpdateAsync(string table, object setDto, object whereDto, CancellationToken ct = default)
    {
        var (sql, param) = _builder.BuildUpdate(table, setDto, whereDto);
        try
        {
            return await _db.ExecuteAsync(sql, param, ct: ct);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to execute UPDATE.", ex);
        }
    }

    public async Task<int> DeleteAsync(string table, object whereDto, CancellationToken ct = default)
    {
        var (sql, param) = _builder.BuildDelete(table, whereDto);
        try
        {
            return await _db.ExecuteAsync(sql, param, ct: ct);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to execute DELETE.", ex);
        }
    }

    public async Task<bool> ExistsAsync(string table, object whereDto, CancellationToken ct = default)
    {
        var (sql, param) = _builder.BuildExists(table, whereDto);
        try
        {
            var result = await _db.ExecuteScalarAsync<int?>(sql, param, ct: ct);
            return result.HasValue;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to execute EXISTS.", ex);
        }
    }

    public async Task<int> InsertAsync(SqlConnection conn, SqlTransaction? tx, string table, object dto, CancellationToken ct = default)
    {
        var (sql, param) = _builder.BuildInsert(table, dto);
        try
        {
            return await _db.ExecuteAsync(conn, tx, sql, param, ct: ct);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to execute INSERT.", ex);
        }
    }

    public async Task<T?> InsertOutputAsync<T>(SqlConnection conn, SqlTransaction? tx, string table, object dto, string identityColumn, CancellationToken ct = default)
    {
        var (sql, param) = _builder.BuildInsertOutput(table, dto, identityColumn);
        try
        {
            return await _db.ExecuteScalarAsync<T>(conn, tx, sql, param, ct: ct);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to execute INSERT with OUTPUT.", ex);
        }
    }

    public async Task<int> UpdateAsync(SqlConnection conn, SqlTransaction? tx, string table, object setDto, object whereDto, CancellationToken ct = default)
    {
        var (sql, param) = _builder.BuildUpdate(table, setDto, whereDto);
        try
        {
            return await _db.ExecuteAsync(conn, tx, sql, param, ct: ct);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to execute UPDATE.", ex);
        }
    }

    public async Task<int> DeleteAsync(SqlConnection conn, SqlTransaction? tx, string table, object whereDto, CancellationToken ct = default)
    {
        var (sql, param) = _builder.BuildDelete(table, whereDto);
        try
        {
            return await _db.ExecuteAsync(conn, tx, sql, param, ct: ct);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to execute DELETE.", ex);
        }
    }

    public async Task<bool> ExistsAsync(SqlConnection conn, SqlTransaction? tx, string table, object whereDto, CancellationToken ct = default)
    {
        var (sql, param) = _builder.BuildExists(table, whereDto);
        try
        {
            var result = await _db.ExecuteScalarAsync<int?>(conn, tx, sql, param, ct: ct);
            return result.HasValue;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to execute EXISTS.", ex);
        }
    }
}

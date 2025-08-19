using System;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using DcMateH5Api.DbExtensions;

namespace DcMateH5Api.Logging;

/// <summary>
/// Persists SQL execution logs to database.
/// </summary>
public class SqlLogService : ISqlLogService
{
    private readonly IDbExecutor _db;
    private readonly IHttpContextAccessor _httpContextAccessor;

    private const string InsertSql = @"INSERT INTO SYS_SQL_LOG (
ID, USER_ID, EXECUTED_AT, DURATION_MS, SQL_TEXT, PARAMETERS,
AFFECTED_ROWS, IP_ADDRESS, ERROR_MESSAGE, IS_SUCCESS)
VALUES (@Id, @UserId, @ExecutedAt, @DurationMs, @SqlText, @Parameters,
@AffectedRows, @IpAddress, @ErrorMessage, @IsSuccess);";

    public SqlLogService(IDbExecutor db, IHttpContextAccessor httpContextAccessor)
    {
        _db = db;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task LogAsync(SqlLogEntry entry, CancellationToken ct = default)
    {
        // Populate missing fields from HTTP context.
        var ctx = _httpContextAccessor.HttpContext;
        if (entry.ExecutedAt == default)
            entry.ExecutedAt = DateTime.UtcNow;
        if (string.IsNullOrEmpty(entry.UserId))
            entry.UserId = ctx?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(entry.IpAddress))
            entry.IpAddress = ctx?.Connection.RemoteIpAddress?.ToString();

        var param = new
        {
            entry.Id,
            entry.UserId,
            entry.ExecutedAt,
            entry.DurationMs,
            entry.SqlText,
            entry.Parameters,
            entry.AffectedRows,
            entry.IpAddress,
            entry.ErrorMessage,
            entry.IsSuccess
        };

        try
        {
            await _db.ExecuteAsync(InsertSql, param, ct: ct);
        }
        catch
        {
            // Swallow logging exceptions to avoid interfering with main flow
        }
    }

    /// <summary>
    /// Helper to serialize parameters consistently.
    /// </summary>
    public static string? SerializeParams(object? param)
    {
        if (param == null)
            return null;
        return JsonSerializer.Serialize(param);
    }
}

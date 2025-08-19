using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using DcMateH5Api.DbExtensions;

namespace DcMateH5Api.Logging;

/// <summary>
/// 提供寫入 SQL 執行記錄的服務介面。
/// </summary>
public interface ISqlLogService
{
    /// <summary>
    /// 寫入一筆 SQL 執行紀錄。
    /// </summary>
    /// <param name="entry">待寫入的紀錄。</param>
    /// <param name="ct">取消權杖。</param>
    Task LogAsync(SqlLogEntry entry, CancellationToken ct = default);
}

/// <summary>
/// 透過 Dapper 將紀錄寫入 <c>SYS_SQL_LOG</c> 表格。
/// </summary>
public sealed class SqlLogService : ISqlLogService
{
    private readonly ISqlConnectionFactory _factory;

    private const string InsertSql =
        @"INSERT INTO SYS_SQL_LOG (ID, USER_ID, EXECUTED_AT, DURATION_MS, SQL_TEXT, PARAMETERS, AFFECTED_ROWS, IP_ADDRESS, ERROR_MESSAGE, IS_SUCCESS)
          VALUES (@Id, @UserId, @ExecutedAt, @DurationMs, @SqlText, @Parameters, @AffectedRows, @IpAddress, @ErrorMessage, @IsSuccess)";

    public SqlLogService(ISqlConnectionFactory factory)
        => _factory = factory;

    public async Task LogAsync(SqlLogEntry entry, CancellationToken ct = default)
    {
        await using var conn = _factory.Create();
        await conn.OpenAsync(ct);
        var cmd = new CommandDefinition(InsertSql, entry, commandType: CommandType.Text, cancellationToken: ct);
        await conn.ExecuteAsync(cmd);
    }
}


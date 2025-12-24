using Dapper;
using DcMateH5Api.Areas.Log.Models;
using DcMateH5Api.DbExtensions;
using System.Data;
using System.Text;
using DcMateH5Api.Areas.Log.Interfaces;

namespace DcMateH5Api.Areas.Log.Services
{
    public class LogService : ILogService
    {
        private readonly ISqlConnectionFactory _factory;

        public LogService(ISqlConnectionFactory factory)
        {
            _factory = factory;
        }

        public async Task LogAsync(SqlLogEntry entry, CancellationToken ct = default)
        {
            await using var conn = _factory.Create();
            await conn.OpenAsync(ct);

            var cmd = new CommandDefinition(
                Sql.InsertSql,
                entry,
                commandType: CommandType.Text,
                cancellationToken: ct);

            await conn.ExecuteAsync(cmd);
        }

        public async Task<IReadOnlyList<SqlLogEntry>> GetLogsAsync(
            SqlLogQuery query,
            CancellationToken ct = default)
        {
            await using var conn = _factory.Create();
            await conn.OpenAsync(ct);

            var sql = new StringBuilder(Sql.SelectBaseSql);
            var parameters = new DynamicParameters();

            // ===== WHERE 組裝 =====
            if (query.UserId.HasValue)
            {
                sql.Append(" AND USER_ID = @UserId");
                parameters.Add("@UserId", query.UserId);
            }

            if (query.RequestId.HasValue)
            {
                sql.Append(" AND REQUEST_ID = @RequestId");
                parameters.Add("@RequestId", query.RequestId);
            }

            if (query.IsSuccess.HasValue)
            {
                sql.Append(" AND IS_SUCCESS = @IsSuccess");
                parameters.Add("@IsSuccess", query.IsSuccess);
            }

            if (query.ExecutedFrom.HasValue)
            {
                sql.Append(" AND EXECUTED_AT >= @ExecutedFrom");
                parameters.Add("@ExecutedFrom", query.ExecutedFrom);
            }

            if (query.ExecutedTo.HasValue)
            {
                sql.Append(" AND EXECUTED_AT < @ExecutedTo");
                parameters.Add("@ExecutedTo", query.ExecutedTo);
            }

            // ===== 排序 + 分頁 =====
            sql.Append(@"
 ORDER BY EXECUTED_AT DESC
 OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY");

            parameters.Add("@Offset", (query.Page - 1) * query.PageSize);
            parameters.Add("@PageSize", query.PageSize);

            var cmd = new CommandDefinition(
                sql.ToString(),
                parameters,
                commandType: CommandType.Text,
                cancellationToken: ct);

            return (await conn.QueryAsync<SqlLogEntry>(cmd)).AsList();
        }

        private static class Sql
        {
            public const string InsertSql = @"
/**/
INSERT INTO SYS_SQL_LOG
(ID, USER_ID, REQUEST_ID, EXECUTED_AT, DURATION_MS, SQL_TEXT, PARAMETERS,
 AFFECTED_ROWS, IP_ADDRESS, ERROR_MESSAGE, IS_SUCCESS)
VALUES
(@Id, @UserId, @RequestId, @ExecutedAt, @DurationMs, @SqlText, @Parameters,
 @AffectedRows, @IpAddress, @ErrorMessage, @IsSuccess)";

            public const string SelectBaseSql = @"
/**/
SELECT
 ID AS Id,
 [USER_ID] AS UserId,
 [REQUEST_ID] AS RequestId,
 [EXECUTED_AT] AS ExecutedAt,
 [DURATION_MS] AS DurationMs,
 [SQL_TEXT] AS SqlText,
 [PARAMETERS] AS [Parameters],
 [AFFECTED_ROWS] AS AffectedRows,
 [IP_ADDRESS] AS IpAddress,
 [ERROR_MESSAGE] AS ErrorMessage,
 [IS_SUCCESS] AS IsSuccess
FROM SYS_SQL_LOG
WHERE 1 = 1";
        }
    }
}

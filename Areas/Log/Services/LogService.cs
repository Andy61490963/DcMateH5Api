
using System.Data;
using Dapper;
using DcMateH5Api.Areas.Log.Interfaces;
using DcMateH5Api.DbExtensions;
using DcMateH5Api.Logging;

namespace DcMateH5Api.Areas.Log.Services
{
    public class LogService : ILogService
    {
        private readonly ISqlConnectionFactory _factory;
        
        public LogService( ISqlConnectionFactory factory)
        {
            _factory = factory;
        }

        public async Task LogAsync(SqlLogEntry entry, CancellationToken ct = default)
        {
            await using var conn = _factory.Create();
            await conn.OpenAsync(ct);
            var cmd = new CommandDefinition(Sql.InsertSql, entry, commandType: CommandType.Text, cancellationToken: ct);
            await conn.ExecuteAsync(cmd);
        }
        
        private static class Sql
        {
            public const string InsertSql =
                @"/**/INSERT INTO SYS_SQL_LOG (ID, USER_ID, REQUEST_ID, EXECUTED_AT, DURATION_MS, SQL_TEXT, PARAMETERS, AFFECTED_ROWS, IP_ADDRESS, ERROR_MESSAGE, IS_SUCCESS)
          VALUES (@Id, @UserId, @RequestId, @ExecutedAt, @DurationMs, @SqlText, @Parameters, @AffectedRows, @IpAddress, @ErrorMessage, @IsSuccess)";
        }
    }
}

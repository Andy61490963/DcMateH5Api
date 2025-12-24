using ClassLibrary;
using DcMateH5Api.Areas.Log.Interfaces;
using DcMateH5Api.Areas.Log.Models;
using DcMateH5Api.DbExtensions;
using DcMateH5Api.Helper;
using DcMateH5Api.Models;
using Microsoft.AspNetCore.Mvc;

namespace DcMateH5Api.Areas.Log.Controllers
{
    /// <summary>
    /// 檢視操作紀錄的地方
    /// </summary>
    [Area("Log")]
    [ApiController]
    [ApiExplorerSettings(GroupName = SwaggerGroups.Log)]
    [Route("[area]/[controller]")]
    [Produces("application/json")]
    public class LogController : ControllerBase
    {
        private readonly ILogService _logService;
        private readonly ISqlConnectionFactory _factory;

        public LogController(ILogService logService, ISqlConnectionFactory factory)
        {
            _logService = logService;
            _factory = factory;
        }

        /// <summary>
        /// 路由常數集中管理，避免魔法字串散落。
        /// </summary>
        private static class Routes
        {
            public const string GetLogs = "sql";
        }

        #region Query - SQL Logs

        /// <summary>
        /// 取得 SQL 執行紀錄（支援條件查詢 + 分頁）
        /// </summary>
        /// <param name="query">查詢條件（UserId/RequestId/IsSuccess/時間區間/分頁）</param>
        /// <param name="ct">CancellationToken</param>
        [HttpGet(Routes.GetLogs)]
        [ProducesResponseType(typeof(Result<IReadOnlyList<SqlLogEntry>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetSqlLogsAsync(
            [FromQuery] SqlLogQuery query,
            CancellationToken ct)
        {
            // ---- 防呆：避免無腦撈爆 DB ----
            if (query.Page < 1)
                query.Page = 1;

            if (query.PageSize < 1)
                query.PageSize = 50;

            // 你可以依需求調整上限，避免一次噴出幾萬筆拖垮 API
            const int maxPageSize = 200;
            if (query.PageSize > maxPageSize)
                query.PageSize = maxPageSize;

            // ---- 防呆：時間區間合理性 ----
            if (query.ExecutedFrom.HasValue && query.ExecutedTo.HasValue &&
                query.ExecutedFrom.Value >= query.ExecutedTo.Value)
            {
                return Ok(Result<IReadOnlyList<SqlLogEntry>>.Fail(
                    LogErrorCode.InvalidParameter,
                    LogErrorCode.InvalidParameter.GetDescription()));
            }

            // ---- 查詢 ----
            var logs = await _logService.GetLogsAsync(query, ct);

            return Ok(Result<IReadOnlyList<SqlLogEntry>>.Ok(logs));
        }

        #endregion
    }
}

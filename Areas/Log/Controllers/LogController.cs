using DcMateH5Api.Areas.Log.Interfaces;
using DcMateH5Api.DbExtensions;
using Microsoft.AspNetCore.Mvc;
using DcMateH5Api.Helper;
using Microsoft.Data.SqlClient;

namespace DcMateH5Api.Areas.Log.Controllers
{
    [Area("Log")]
    [ApiController]
    [ApiExplorerSettings(GroupName = SwaggerGroups.Log)]
    [Route("[area]/[controller]")]
    // [Produces("application/json")]
    public class LogController : ControllerBase
    {
        private readonly ILogService _logService;
        private readonly ISqlConnectionFactory _factory;

        public LogController(ILogService logService, ISqlConnectionFactory factory)
        {
            _logService = logService;
            _factory = factory;
        }
        
        // [HttpPost("GetLog")]
        // public IActionResult GetLogs()
        // {
        //     var logs = _logService.GetLogList();
        //     return Ok(logs);
        // }
        [HttpGet]
        public IActionResult Get() => Ok(new {
            Instance = Environment.MachineName,
            TimeUtc = DateTime.UtcNow
        });
        
        /// <summary>
        /// 測試 LogService 使用的 DB 連線是否正常
        /// </summary>
        [HttpGet("test-log-connection")]
        public async Task<IActionResult> TestLogConnection(
            [FromServices] ISqlConnectionFactory factory,
            CancellationToken ct)
        {
            try
            {
                var conn = factory.Create(); // 不先 using，先把 connectionString 拿出來
                var csb = new SqlConnectionStringBuilder(conn.ConnectionString);

                await using (conn)
                {
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    await conn.OpenAsync(ct);
                    sw.Stop();

                    await using var cmd = conn.CreateCommand();
                    cmd.CommandText = "SELECT GETDATE()";
                    var result = await cmd.ExecuteScalarAsync(ct);

                    return Ok(new
                    {
                        success = true,
                        message = "DB 連線正常",
                        dataSource = csb.DataSource,         // << 看這裡
                        initialCatalog = csb.InitialCatalog, // << 要是 DCMATE-H5-NEW
                        userId = csb.UserID,
                        openConnectionMs = sw.ElapsedMilliseconds,
                        serverTime = result
                    });
                }
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    type = "SqlException",
                    error = ex.Message,
                    number = ex.Number,
                    state = ex.State,
                    classLevel = ex.Class
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    type = ex.GetType().Name,
                    error = ex.Message
                });
            }
        }


    }
    
}

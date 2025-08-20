using DcMateH5Api.Areas.Log.Interfaces;
using Microsoft.AspNetCore.Mvc;
using DcMateH5Api.Helper;

namespace DcMateH5Api.Areas.Log.Controllers
{
    [Area("Log")]
    [ApiController]
    [ApiExplorerSettings(GroupName = SwaggerGroups.Permission)]
    [Route("[area]/[controller]")]
    [Produces("application/json")]
    public class LogController : ControllerBase
    {
        private readonly ILogService _logService;

        public LogController(ILogService logService)
        {
            _logService = logService;
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
    }
}

using DcMateH5Api.Areas.Log.Interfaces;
using DcMateH5Api.DbExtensions;
using Microsoft.AspNetCore.Mvc;
using DcMateH5Api.Helper;

namespace DcMateH5Api.Areas.Log.Controllers;

/// <summary>
/// 檢視操作紀錄的地方 (目前還沒開發)
/// </summary>
[Area("Log")]
[ApiController]
[ApiExplorerSettings(GroupName = SwaggerGroups.Log)]
[Route("[area]/[controller]")]
public class LogController : ControllerBase
{
    private readonly ILogService _logService;
    private readonly ISqlConnectionFactory _factory;

    public LogController(ILogService logService, ISqlConnectionFactory factory)
    {
        _logService = logService;
        _factory = factory;
    }
}
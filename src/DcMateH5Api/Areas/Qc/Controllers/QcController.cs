using DcMateClassLibrary.Helper;
using DcMateH5.Abstractions.Qc;
using DcMateH5.Abstractions.Qc.Models;
using Microsoft.AspNetCore.Mvc;

namespace DcMateH5Api.Areas.Qc.Controllers;

[Area(Routes.AreaName)]
[Route(Routes.Base)]
[ApiExplorerSettings(GroupName = SwaggerGroups.Qc)]
[ApiController]
public class QcController : ControllerBase
{
    private readonly IQcService _qcService;
    private readonly ILogger<QcController> _logger;

    public QcController(IQcService qcService, ILogger<QcController> logger)
    {
        _qcService = qcService;
        _logger = logger;
    }

    [HttpPost(Routes.CreateBatch)]
    [ProducesResponseType(typeof(QcBatchCreateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(QcErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateBatch([FromBody] QcBatchCreateRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await _qcService.CreateBatchAsync(request, ct));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Qc batch creation failed");
            return StatusCode(StatusCodes.Status500InternalServerError, new QcErrorResponse { Message = ex.Message });
        }
    }

    private static class Routes
    {
        public const string AreaName = "Qc";
        public const string Base = "api/[area]/[controller]";
        public const string CreateBatch = "CreateBatch";
    }
}

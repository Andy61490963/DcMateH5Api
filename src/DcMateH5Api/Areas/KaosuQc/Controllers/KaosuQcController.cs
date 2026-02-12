using DcMateClassLibrary.Helper;
using DcMateH5.Abstractions.KaosuQc;
using DcMateH5.Abstractions.KaosuQc.Models;
using Microsoft.AspNetCore.Mvc;

namespace DcMateH5Api.Areas.KaosuQc.Controllers;

[Area(Routes.AreaName)]
[Route(Routes.Base)]
[ApiExplorerSettings(GroupName = SwaggerGroups.ZZKaosuQc)]
[ApiController]
public class KaosuQcController : ControllerBase
{
    private readonly IKaosuQcService _kaosuQcService;
    private readonly ILogger<KaosuQcController> _logger;

    public KaosuQcController(IKaosuQcService kaosuQcService, ILogger<KaosuQcController> logger)
    {
        _kaosuQcService = kaosuQcService;
        _logger = logger;
    }

    /// <summary>
    /// 批次新增 Kaosu 品檢資料（單頭 + 多筆單身）。
    /// </summary>
    [HttpPost(Routes.CreateBatch)]
    [ProducesResponseType(typeof(KaosuQcBatchCreateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(KaosuQcErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateBatch([FromBody] KaosuQcBatchCreateRequest request, CancellationToken ct)
    {
        try
        {
            var response = await _kaosuQcService.CreateBatchAsync(request, ct);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "KaosuQc 批次新增失敗");
            return StatusCode(StatusCodes.Status500InternalServerError, new KaosuQcErrorResponse
            {
                Message = ex.Message
            });
        }
    }

    private static class Routes
    {
        public const string AreaName = "KaosuQc";
        public const string Base = "api/[area]/[controller]";
        public const string CreateBatch = "CreateBatch";
    }
}

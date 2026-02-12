using DcMateClassLibrary.Helper;
using DcMateH5.Abstractions.KaosuQc;
using DcMateH5.Abstractions.KaosuQc.Models;
using Microsoft.AspNetCore.Mvc;

namespace DcMateH5Api.Areas.KaosuQc.Controllers;

[Area(Routes.AreaName)]
[Route(Routes.Base)]
[ApiExplorerSettings(GroupName = SwaggerGroups.Wip)]
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
    /// <remarks>
    /// 業務規則：
    /// 1. 一個 request 可一次送入多個單頭，每個單頭可包含多筆單身。
    /// 2. 全部資料在同一交易中寫入，任一單頭/單身失敗即 rollback。
    /// 3. 若 INSPECTION_NO 已存在，視為失敗並 rollback。
    /// 4. 單身 INSPECTION_NO 一律以單頭 INSPECTION_NO 覆蓋。
    /// </remarks>
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
                Message = "新增失敗，請稍後再試或聯繫系統管理員。"
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

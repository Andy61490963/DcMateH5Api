using DcMateH5Api.Helper;
using DcMateH5Api.Areas.RouteOperation.Interfaces;
using DcMateH5Api.Areas.RouteOperation.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace DcMateH5Api.Areas.RouteOperation.Controllers
{
    /// <summary>
    /// 提供製程 Route 主檔、工作站節點、條件定義與條件關聯設定的 API。
    /// </summary>
    [Area("RouteOperation")]
    [ApiController]
    [ApiExplorerSettings(GroupName = SwaggerGroups.RouteOperation)]
    [Route("[area]/[controller]")]
    [Produces("application/json")]
    public class RouteOperationController : ControllerBase
    {
        private readonly IRouteOperationService _routeOperationService;
        private readonly IBasRouteService _routeService;
        private readonly IBasOperationService _operationService;
        private readonly IBasConditionService _conditionService;

        /// <summary>
        /// 路由常數集中管理，避免魔法字串散落。
        /// </summary>
        private static class Routes
        {
            // ===== Route 主檔（BAS_ROUTE） =====
            public const string RouteMasterList  = "masters/routes";
            public const string RouteMasterBySid = "masters/routes/{sid:decimal}";

            // ===== Operation 主檔（BAS_OPERATION） =====
            public const string OperationMasterList  = "masters/operations";
            public const string OperationMasterBySid = "masters/operations/{sid:decimal}";

            // ===== Condition 定義主檔（BAS_CONDITION） =====
            public const string ConditionMasterList  = "masters/conditions";
            public const string ConditionMasterBySid = "masters/conditions/{sid:decimal}";

            // ===== Route 組態查詢 =====
            public const string RouteConfig        = "routes/{routeSid:decimal}/config";

            // ===== 工作站節點（BAS_ROUTE_OPERATION） =====
            public const string RouteOperations    = "routes/{routeSid:decimal}/operations";
            public const string RouteOperationById = "routes/{routeSid:decimal}/operations/{routeOperationSid:decimal}";

            // ===== Extra 工作站節點（BAS_ROUTE_OPERATION_EXTRA） =====
            public const string ExtraRouteOperations    = "routes/{routeSid:decimal}/extra-operations";
            public const string ExtraRouteOperationById = "routes/{routeSid:decimal}/extra-operations/{routeOperationSid:decimal}";
            
            // ===== 條件設定（BAS_ROUTE_OPERATION_CONDITION） =====
            public const string Conditions         = "operations/{routeOperationSid:decimal}/conditions";
            public const string ConditionById      = "operations/{routeOperationSid:decimal}/conditions/{conditionSid:decimal}";
        }

        public RouteOperationController(
            IRouteOperationService routeOperationService,
            IBasRouteService routeService,
            IBasOperationService operationService,
            IBasConditionService conditionService)
        {
            _routeOperationService = routeOperationService;
            _routeService = routeService;
            _operationService = operationService;
            _conditionService = conditionService;
        }

        #region Route 主檔 CRUD（BAS_ROUTE）

        /// <summary>取得所有 Route 主檔(BAS_ROUTE)</summary>
        [HttpGet(Routes.RouteMasterList)]
        [ProducesResponseType(typeof(List<RouteViewModel>), StatusCodes.Status200OK)]
        public async Task<ActionResult<List<RouteViewModel>>> GetRouteMasters(CancellationToken ct)
        {
            var list = await _routeService.GetAllAsync(ct);
            return Ok(list);
        }

        /// <summary>取得單一 Route 主檔。</summary>
        [HttpGet(Routes.RouteMasterBySid)]
        [ProducesResponseType(typeof(RouteViewModel), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<RouteViewModel>> GetRouteMaster([FromRoute] decimal sid, CancellationToken ct)
        {
            var item = await _routeService.GetAsync(sid, ct);
            return item is null ? NotFound() : Ok(item);
        }

        /// <summary>建立新的 Route 主檔。</summary>
        [HttpPost(Routes.RouteMasterList)]
        [ProducesResponseType(typeof(RouteViewModel), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<ActionResult> CreateRouteMaster(
            [FromBody] CreateRouteRequest request,
            CancellationToken ct)
        {
            if (await _routeService.RouteCodeExistsAsync(request.RouteCode, ct))
            {
                return Conflict($"RouteCode 已存在：{request.RouteCode}");
            }

            var sid = await _routeService.CreateAsync(request, ct);
            return CreatedAtAction(nameof(GetRouteMaster), new { sid }, null);
        }

        /// <summary>更新 Route 主檔。</summary>
        [HttpPut(Routes.RouteMasterBySid)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> UpdateRouteMaster(
            [FromRoute] decimal sid,
            [FromBody] UpdateRouteRequest request,
            CancellationToken ct)
        {
            var existing = await _routeService.GetAsync(sid, ct);
            if (existing is null) return NotFound();

            if (request.RouteCode != null &&
                await _routeService.RouteCodeExistsAsync(request.RouteCode, ct, sid))
            {
                return Conflict($"RouteCode 已存在：{request.RouteCode}");
            }

            await _routeService.UpdateAsync(sid, request, ct);
            return NoContent();
        }

        /// <summary>刪除 Route 主檔。</summary>
        [HttpDelete(Routes.RouteMasterBySid)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteRouteMaster([FromRoute] decimal sid, CancellationToken ct)
        {
            var existing = await _routeService.GetAsync(sid, ct);
            if (existing is null) return NotFound();

            await _routeService.DeleteAsync(sid, ct);
            return NoContent();
        }

        #endregion

        #region Operation 主檔 CRUD（BAS_OPERATION）

        /// <summary>取得所有工作站主檔 (BAS_OPERATION)。</summary>
        [HttpGet(Routes.OperationMasterList)]
        [ProducesResponseType(typeof(List<OperationViewModel>), StatusCodes.Status200OK)]
        public async Task<ActionResult<List<OperationViewModel>>> GetOperationMasters(CancellationToken ct)
        {
            var list = await _operationService.GetAllAsync(ct);
            return Ok(list);
        }

        /// <summary>取得單一工作站主檔。</summary>
        [HttpGet(Routes.OperationMasterBySid)]
        [ProducesResponseType(typeof(OperationViewModel), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<OperationViewModel>> GetOperationMaster([FromRoute] decimal sid, CancellationToken ct)
        {
            var item = await _operationService.GetAsync(sid, ct);
            return item is null ? NotFound() : Ok(item);
        }

        /// <summary>建立新的工作站主檔。</summary>
        [HttpPost(Routes.OperationMasterList)]
        [ProducesResponseType(typeof(OperationViewModel), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<ActionResult> CreateOperationMaster(
            [FromBody] CreateOperationRequest request,
            CancellationToken ct)
        {
            if (request.OperationType != "Normal" && request.OperationType != "Repair")
            {
                return ValidationProblem($"無效的 OperationType：{request.OperationType}（允許值：Normal / Repair）");
            }

            if (await _operationService.OperationCodeExistsAsync(request.OperationCode, ct))
            {
                return Conflict($"OperationCode 已存在：{request.OperationCode}");
            }

            var sid = await _operationService.CreateAsync(request, ct);
            return CreatedAtAction(nameof(GetOperationMaster), new { sid }, null);
        }

        /// <summary>更新工作站主檔。</summary>
        [HttpPut(Routes.OperationMasterBySid)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> UpdateOperationMaster(
            [FromRoute] decimal sid,
            [FromBody] UpdateOperationRequest request,
            CancellationToken ct)
        {
            var existing = await _operationService.GetAsync(sid, ct);
            if (existing is null) return NotFound();

            if (request.OperationType != null &&
                request.OperationType != "Normal" &&
                request.OperationType != "Repair")
            {
                return ValidationProblem($"無效的 OperationType：{request.OperationType}（允許值：Normal / Repair）");
            }

            if (request.OperationCode != null &&
                await _operationService.OperationCodeExistsAsync(request.OperationCode, ct, sid))
            {
                return Conflict($"OperationCode 已存在：{request.OperationCode}");
            }

            await _operationService.UpdateAsync(sid, request, ct);
            return NoContent();
        }

        /// <summary>刪除工作站主檔。</summary>
        [HttpDelete(Routes.OperationMasterBySid)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteOperationMaster([FromRoute] decimal sid, CancellationToken ct)
        {
            var existing = await _operationService.GetAsync(sid, ct);
            if (existing is null) return NotFound();

            await _operationService.DeleteAsync(sid, ct);
            return NoContent();
        }

        #endregion

        #region Condition 主檔 CRUD（BAS_CONDITION 定義）

        /// <summary>取得所有條件定義主檔 (BAS_CONDITION)。</summary>
        [HttpGet(Routes.ConditionMasterList)]
        [ProducesResponseType(typeof(List<ConditionViewModel>), StatusCodes.Status200OK)]
        public async Task<ActionResult<List<ConditionViewModel>>> GetConditionMasters(CancellationToken ct)
        {
            var list = await _conditionService.GetAllAsync(ct);
            return Ok(list);
        }

        /// <summary>取得單一條件定義主檔。</summary>
        [HttpGet(Routes.ConditionMasterBySid)]
        [ProducesResponseType(typeof(ConditionViewModel), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ConditionViewModel>> GetConditionMaster([FromRoute] decimal sid, CancellationToken ct)
        {
            var item = await _conditionService.GetAsync(sid, ct);
            return item is null ? NotFound() : Ok(item);
        }

        /// <summary>建立新的條件定義主檔。</summary>
        [HttpPost(Routes.ConditionMasterList)]
        [ProducesResponseType(typeof(ConditionViewModel), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<ActionResult> CreateConditionMaster(
            [FromBody] CreateConditionRequest request,
            CancellationToken ct)
        {
            if (await _conditionService.ConditionCodeExistsAsync(request.ConditionCode, ct))
            {
                return Conflict($"ConditionCode 已存在：{request.ConditionCode}");
            }

            var sid = await _conditionService.CreateAsync(request, ct);
            return CreatedAtAction(nameof(GetConditionMaster), new { sid }, null);
        }

        /// <summary>更新條件定義主檔。</summary>
        [HttpPut(Routes.ConditionMasterBySid)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> UpdateConditionMaster(
            [FromRoute] decimal sid,
            [FromBody] UpdateConditionRequest request,
            CancellationToken ct)
        {
            var existing = await _conditionService.GetAsync(sid, ct);
            if (existing is null) return NotFound();

            if (request.ConditionCode != null &&
                await _conditionService.ConditionCodeExistsAsync(request.ConditionCode, ct, sid))
            {
                return Conflict($"ConditionCode 已存在：{request.ConditionCode}");
            }

            await _conditionService.UpdateAsync(sid, request, ct);
            return NoContent();
        }

        /// <summary>刪除條件定義主檔。</summary>
        [HttpDelete(Routes.ConditionMasterBySid)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteConditionMaster([FromRoute] decimal sid, CancellationToken ct)
        {
            var existing = await _conditionService.GetAsync(sid, ct);
            if (existing is null) return NotFound();

            await _conditionService.DeleteAsync(sid, ct);
            return NoContent();
        }

        #endregion

        #region Route 組態查詢

        /// <summary>
        /// 取得指定 Route 的完整組態（主線站別 + 條件 + Extra 站）。
        /// </summary>
        /// <remarks>
        /// 前端「流程工作站設定畫面」進場時，可以直接呼叫這支一次載入完整結構。
        /// </remarks>
        [HttpGet(Routes.RouteConfig)]
        [ProducesResponseType(typeof(RouteConfigViewModel), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<RouteConfigViewModel>> GetRouteConfig(
            [FromRoute] decimal routeSid,
            CancellationToken ct)
        {
            var config = await _routeOperationService.GetRouteConfigAsync(routeSid, ct);
            return config is null ? NotFound() : Ok(config);
        }

        #endregion

        #region RouteOperation CRUD（常規流程工作站節點）

        /// <summary>
        /// 在指定 Route 底下新增一個工作站節點（BAS_ROUTE_OPERATION）。
        /// </summary>
        [HttpPost(Routes.RouteOperations)]
        [ProducesResponseType(typeof(RouteOperationDetailViewModel), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<ActionResult<RouteOperationDetailViewModel>> CreateRouteOperation(
            [FromRoute] decimal routeSid,
            [FromBody] CreateRouteOperationRequest request,
            CancellationToken ct)
        {
            // 防止前端亂塞 RouteSid
            if (request.RouteSid != routeSid)
            {
                return ValidationProblem("RouteSid 與路由參數不一致。");
            }

            if (!await _routeOperationService.RouteExistsAsync(routeSid, ct))
            {
                return NotFound($"找不到指定 Route：{routeSid}");
            }

            if (!await _routeOperationService.OperationExistsAsync(request.OperationSid, ct))
            {
                return ValidationProblem($"不存在的工作站 OperationSid：{request.OperationSid}");
            }

            // // 一條 Route 裡 SEQ 不可重複
            // if (await _routeOperationService.SeqExistsAsync(routeSid, request.Seq, ct))
            // {
            //     return Conflict($"在 Route {routeSid} 中，流程順序 SEQ = {request.Seq} 已被使用。");
            // }

            var newSid = await _routeOperationService.CreateRouteOperationAsync(request, ct);
            var detail = await _routeOperationService.GetRouteOperationAsync(newSid, ct);

            return CreatedAtAction(nameof(GetRouteOperation),
                new { routeSid, routeOperationSid = newSid },
                detail);
        }

        /// <summary>
        /// 取得單一工作站節點詳細資訊（BAS_ROUTE_OPERATION）。
        /// </summary>
        [HttpGet(Routes.RouteOperationById)]
        [ProducesResponseType(typeof(RouteOperationDetailViewModel), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<RouteOperationDetailViewModel>> GetRouteOperation(
            [FromRoute] decimal routeSid,
            [FromRoute] decimal routeOperationSid,
            CancellationToken ct)
        {
            var item = await _routeOperationService.GetRouteOperationAsync(routeOperationSid, ct);
            if (item is null)
            {
                return NotFound();
            }

            return Ok(item);
        }

        /// <summary>
        /// 更新指定工作站節點（BAS_ROUTE_OPERATION）。
        /// </summary>
        [HttpPut(Routes.RouteOperationById)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> UpdateRouteOperation(
            [FromRoute] decimal routeSid,
            [FromRoute] decimal routeOperationSid,
            [FromBody] UpdateRouteOperationRequest request,
            CancellationToken ct)
        {
            var existing = await _routeOperationService.GetRouteOperationAsync(routeOperationSid, ct);
            if (existing is null || existing.RouteSid != routeSid)
            {
                return NotFound();
            }

            // 如果有調整 SEQ，要檢查同 Route 內是否衝突
            // if (request.Seq.HasValue &&
            //     await _routeOperationService.SeqExistsAsync(routeSid, request.Seq.Value, ct, routeOperationSid))
            // {
            //     return Conflict($"在 Route {routeSid} 中，流程順序 SEQ = {request.Seq} 已被使用。");
            // }

            await _routeOperationService.UpdateRouteOperationAsync(routeOperationSid, request, ct);
            return NoContent();
        }

        /// <summary>
        /// 刪除指定工作站節點（BAS_ROUTE_OPERATION）。
        /// </summary>
        [HttpDelete(Routes.RouteOperationById)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteRouteOperation(
            [FromRoute] decimal routeSid,
            [FromRoute] decimal routeOperationSid,
            CancellationToken ct)
        {
            var existing = await _routeOperationService.GetRouteOperationAsync(routeOperationSid, ct);
            if (existing is null || existing.RouteSid != routeSid)
            {
                return NotFound();
            }

            await _routeOperationService.DeleteRouteOperationAsync(routeOperationSid, ct);
            return NoContent();
        }

        #endregion

        #region RouteOperation CRUD（Extra流程工作站節點）

        /// <summary>
        /// 在指定 Route 底下新增一個工作站節點（BAS_ROUTE_OPERATION_EXTRA）。
        /// </summary>
        [HttpPost(Routes.ExtraRouteOperations)]
        [ProducesResponseType(typeof(RouteOperationDetailViewModel), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<ActionResult<RouteExtraOperationDetailViewModel>> CreateExtraRouteOperation(
            [FromRoute] decimal routeSid,
            [FromBody] CreateRouteExtraOperationRequest request,
            CancellationToken ct)
        {
            // 防止前端亂塞 RouteSid
            if (request.RouteSid != routeSid)
            {
                return ValidationProblem("RouteSid 與路由參數不一致。");
            }

            if (!await _routeOperationService.RouteExistsAsync(routeSid, ct))
            {
                return NotFound($"找不到指定 Route：{routeSid}");
            }

            if (!await _routeOperationService.OperationExistsAsync(request.OperationSid, ct))
            {
                return ValidationProblem($"不存在的工作站 OperationSid：{request.OperationSid}");
            }

            var newSid = await _routeOperationService.CreateRouteExtraOperationAsync(request, ct);
            var detail = await _routeOperationService.GetRouteExtraOperationAsync(newSid, ct);

            return CreatedAtAction(
                nameof(GetRouteExtraOperation),
                new { routeSid, routeOperationSid = newSid },
                detail);
        }

        /// <summary>
        /// 取得單一工作站節點詳細資訊(BAS_ROUTE_OPERATION_EXTRA)。
        /// </summary>
        [HttpGet(Routes.ExtraRouteOperationById)]
        [ProducesResponseType(typeof(RouteOperationDetailViewModel), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<RouteExtraOperationDetailViewModel>> GetRouteExtraOperation(
            [FromRoute] decimal routeSid,
            [FromRoute] decimal routeOperationSid,
            CancellationToken ct)
        {
            var item = await _routeOperationService.GetRouteExtraOperationAsync(routeOperationSid, ct);
            if (item is null)
            {
                return NotFound();
            }

            return Ok(item);
        }

        /// <summary>
        /// 刪除指定工作站節點(BAS_ROUTE_OPERATION_EXTRA)。
        /// </summary>
        [HttpDelete(Routes.ExtraRouteOperationById)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteRouteExtraOperation(
            [FromRoute] decimal routeSid,
            [FromRoute] decimal routeOperationSid,
            CancellationToken ct)
        {
            var existing = await _routeOperationService.GetRouteExtraOperationAsync(routeOperationSid, ct);
            if (existing is null || existing.RouteSid != routeSid)
            {
                return NotFound();
            }

            await _routeOperationService.DeleteRouteExtraOperationAsync(routeOperationSid, ct);
            return NoContent();
        }

        #endregion
        
        #region Condition CRUD（BAS_ROUTE_OPERATION_CONDITION）

        /// <summary>
        /// 在指定工作站節點下新增一筆條件設定。
        /// </summary>
        [HttpPost(Routes.Conditions)]
        [ProducesResponseType(typeof(RouteOperationConditionViewModel), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<ActionResult<RouteOperationConditionViewModel>> CreateCondition(
            [FromRoute] decimal routeOperationSid,
            [FromBody] CreateRouteOperationConditionRequest request,
            CancellationToken ct)
        {
            if (request.RouteOperationSid != routeOperationSid)
            {
                return ValidationProblem("RouteOperationSid 與路由參數不一致。");
            }

            if (!await _routeOperationService.RouteOperationExistsAsync(routeOperationSid, ct))
            {
                return NotFound($"找不到指定工作站節點：{routeOperationSid}");
            }

            if (!await _routeOperationService.ConditionDefinitionExistsAsync(request.ConditionSid, ct))
            {
                return ValidationProblem($"不存在的條件定義 BAS_CONDITION_SID：{request.ConditionSid}");
            }

            if (request.NextRouteOperationSid.HasValue &&
                !await _routeOperationService.RouteOperationExistsAsync(request.NextRouteOperationSid.Value, ct))
            {
                return ValidationProblem($"不存在的下一站 RouteOperation：{request.NextRouteOperationSid.Value}");
            }

            if (request.NextRouteExtraOperationSid.HasValue &&
                !await _routeOperationService.ExtraOperationExistsAsync(request.NextRouteExtraOperationSid.Value, ct))
            {
                return ValidationProblem($"不存在的額外站 RouteOperationExtra：{request.NextRouteExtraOperationSid.Value}");
            }

            // if (await _routeOperationService.ConditionSeqExistsAsync(routeOperationSid, request.Seq, ct))
            // {
            //     return Conflict($"該工作站下，條件順序 SEQ = {request.Seq} 已被使用。");
            // }

            var newSid = await _routeOperationService.CreateConditionAsync(request, ct);
            var detail = await _routeOperationService.GetConditionAsync(newSid, ct);

            return CreatedAtAction(nameof(GetCondition),
                new { routeOperationSid, conditionSid = newSid },
                detail);
        }

        /// <summary>
        /// 取得指定條件設定內容(conditionSid 是 BAS_ROUTE_OPERATION_CONDITION 的 SID)。
        /// </summary>
        [HttpGet(Routes.ConditionById)]
        [ProducesResponseType(typeof(RouteOperationConditionViewModel), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<RouteOperationConditionViewModel>> GetCondition(
            [FromRoute] decimal routeOperationSid,
            [FromRoute] decimal conditionSid,
            CancellationToken ct)
        {
            var cond = await _routeOperationService.GetConditionAsync(conditionSid, ct);
            if (cond is null || cond.RouteOperationSid != routeOperationSid)
            {
                return NotFound();
            }

            return Ok(cond);
        }

        /// <summary>
        /// 更新指定條件設定(conditionSid 是 BAS_ROUTE_OPERATION_CONDITION 的 SID)。
        /// </summary>
        [HttpPut(Routes.ConditionById)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> UpdateCondition(
            [FromRoute] decimal routeOperationSid,
            [FromRoute] decimal conditionSid,
            [FromBody] UpdateRouteOperationConditionRequest request,
            CancellationToken ct)
        {
            var existing = await _routeOperationService.GetConditionAsync(conditionSid, ct);
            if (existing is null || existing.RouteOperationSid != routeOperationSid)
            {
                return NotFound();
            }

            // if (request.Seq.HasValue &&
            //     await _routeOperationService.ConditionSeqExistsAsync(routeOperationSid, request.Seq.Value, ct, conditionSid))
            // {
            //     return Conflict($"該工作站下，條件順序 SEQ = {request.Seq} 已被使用。");
            // }

            if (request.NextRouteOperationSid.HasValue &&
                !await _routeOperationService.RouteOperationExistsAsync(request.NextRouteOperationSid.Value, ct))
            {
                return ValidationProblem($"不存在的下一站 RouteOperation：{request.NextRouteOperationSid.Value}");
            }

            if (request.NextRouteExtraOperationSid.HasValue &&
                !await _routeOperationService.ExtraOperationExistsAsync(request.NextRouteExtraOperationSid.Value, ct))
            {
                return ValidationProblem($"不存在的額外站 RouteOperationExtra：{request.NextRouteExtraOperationSid.Value}");
            }

            await _routeOperationService.UpdateConditionAsync(conditionSid, request, ct);
            return NoContent();
        }

        /// <summary>
        /// 刪除指定條件設定。
        /// </summary>
        [HttpDelete(Routes.ConditionById)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteCondition(
            [FromRoute] decimal routeOperationSid,
            [FromRoute] decimal conditionSid,
            CancellationToken ct)
        {
            var existing = await _routeOperationService.GetConditionAsync(conditionSid, ct);
            if (existing is null || existing.RouteOperationSid != routeOperationSid)
            {
                return NotFound();
            }

            await _routeOperationService.DeleteConditionAsync(conditionSid, ct);
            return NoContent();
        }

        #endregion
    }
}

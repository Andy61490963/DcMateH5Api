
using Dapper;
using DcMateH5Api.Areas.RouteOperation.Interfaces;
using DcMateH5Api.Areas.RouteOperation.Mappers;
using DcMateH5Api.Areas.RouteOperation.ViewModels;
using DcMateH5Api.Services.Cache;
using DcMateH5Api.SqlHelper;
using Microsoft.Data.SqlClient;
using System.Linq;
using DcMateH5Api.Areas.RouteOperation.Models;

namespace DcMateH5Api.Areas.RouteOperation.Services
{
    /// <summary>
    /// Route 工作站與條件設定的服務層實作。
    /// 使用 SQLGenerateHelper + Dapper 操作 BAS_ROUTE_* 與 BAS_CONDITION。
    /// </summary>
    public class RouteOperationService : IRouteOperationService
    {
        private readonly SQLGenerateHelper _sqlHelper;
        private readonly IDbExecutor _db;
        private readonly SqlConnection _con;
        private readonly ICacheService _cache;

        public RouteOperationService(
            SQLGenerateHelper sqlHelper,
            IDbExecutor db,
            SqlConnection con,
            ICacheService cache)
        {
            _sqlHelper = sqlHelper;
            _db = db;
            _con = con;
            _cache = cache;
        }

        #region Route Exists / Operation Exists / Extra Exists / ConditionDefinition Exists

        public async Task<bool> RouteExistsAsync(decimal routeSid, CancellationToken ct)
        {
            var where = new WhereBuilder<BAS_ROUTE>()
                .AndEq(x => x.SID, routeSid);
            var route = await _sqlHelper.SelectFirstOrDefaultAsync(where, ct);
            return route is not null;
        }

        public async Task<bool> OperationExistsAsync(decimal operationSid, CancellationToken ct)
        {
            var where = new WhereBuilder<BAS_OPERATION>()
                .AndEq(x => x.SID, operationSid);
            var op = await _sqlHelper.SelectFirstOrDefaultAsync(where, ct);
            return op is not null;
        }

        public async Task<bool> ExtraOperationExistsAsync(decimal extraSid, CancellationToken ct)
        {
            var where = new WhereBuilder<BAS_ROUTE_OPERATION_EXTRA>()
                .AndEq(x => x.SID, extraSid);
            var extra = await _sqlHelper.SelectFirstOrDefaultAsync(where, ct);
            return extra is not null;
        }

        public async Task<bool> ConditionDefinitionExistsAsync(decimal conditionSid, CancellationToken ct)
        {
            var where = new WhereBuilder<BAS_CONDITION>()
                .AndEq(x => x.SID, conditionSid);
            var cond = await _sqlHelper.SelectFirstOrDefaultAsync(where, ct);
            return cond is not null;
        }

        public async Task<bool> RouteOperationExistsAsync(decimal routeOperationSid, CancellationToken ct)
        {
            var where = new WhereBuilder<BAS_ROUTE_OPERATION>()
                .AndEq(x => x.SID, routeOperationSid);
            var op = await _sqlHelper.SelectFirstOrDefaultAsync(where, ct);
            return op is not null;
        }

        #endregion

        #region RouteConfig

        /// <summary>
        /// 取得指定 Route 的完整組態（主線站別 + 條件 + Extra 站）。
        /// </summary>
        public async Task<RouteConfigViewModel?> GetRouteConfigAsync(decimal routeSid, CancellationToken ct)
        {
            // 1. Route 本體
            var routeWhere = new WhereBuilder<BAS_ROUTE>()
                .AndEq(x => x.SID, routeSid).AndNotDeleted();
            var route = await _sqlHelper.SelectFirstOrDefaultAsync(routeWhere, ct);
            if (route is null) return null;

            var result = new RouteConfigViewModel
            {
                RouteSid = route.SID,
                RouteCode = route.ROUTE_CODE,
                RouteName = route.ROUTE_NAME
            };

            // 2. 主線站別（BAS_ROUTE_OPERATION + BAS_OPERATION）
            const string sqlOperations = @"
SELECT 
    ro.SID               AS RouteOperationSid,
    ro.BAS_ROUTE_SID     AS RouteSid,
    ro.BAS_OPERATION_SID AS OperationSid,
    ro.SEQ,
    ro.ERP_STAGE         AS ErpStage,
    ro.END_FLAG          AS EndFlag,
    op.OPERATION_CODE    AS OperationCode,
    op.OPERATION_NAME    AS OperationName
FROM BAS_ROUTE_OPERATION ro
JOIN BAS_OPERATION op 
    ON op.SID = ro.BAS_OPERATION_SID
   AND op.IS_DELETE = 0
WHERE ro.BAS_ROUTE_SID = @RouteSid
  AND ro.IS_DELETE = 0
ORDER BY ro.SEQ;";

            var operations = (await _db.QueryAsync<RouteOperationDetailViewModel>(
                sqlOperations,
                new { RouteSid = routeSid },
                timeoutSeconds: 30,
                ct: ct)).ToList();

            // 3. Extra 站（BAS_ROUTE_OPERATION_EXTRA + BAS_OPERATION）
            const string sqlExtra = @"
SELECT 
    extra.SID               AS RouteExtraSid,
    extra.BAS_ROUTE_SID     AS RouteSid,
    extra.BAS_OPERATION_SID AS OperationSid,
    op.OPERATION_CODE       AS OperationCode,
    op.OPERATION_NAME       AS OperationName
FROM BAS_ROUTE_OPERATION_EXTRA extra
JOIN BAS_OPERATION op 
    ON op.SID = extra.BAS_OPERATION_SID
   AND op.IS_DELETE = 0
WHERE extra.BAS_ROUTE_SID = @RouteSid
  AND extra.IS_DELETE = 0;";

            var extraOperations = (await _db.QueryAsync<RouteExtraOperationViewModel>(
                sqlExtra,
                new { RouteSid = routeSid },
                timeoutSeconds: 30,
                ct: ct)).ToList();

            // 4. 條件（BAS_ROUTE_OPERATION_CONDITION + BAS_CONDITION + 下一站 + Extra）
            const string sqlConditions = @"
SELECT 
    roc.SID                          AS ConditionSid,
    roc.BAS_ROUTE_OPERATION_SID      AS RouteOperationSid,
    roc.BAS_CONDITION_SID            AS ConditionDefinitionSid,
    ISNULL(roc.SEQ, 0)               AS Seq,
    roc.NEXT_ROUTE_OPERATION_SID     AS NextRouteOperationSid,
    roc.NEXT_ROUTE_EXTRA_OPERATION_SID AS NextRouteExtraOperationSid,
    roc.HOLD                         AS Hold,

    c.CONDITION_CODE                 AS ConditionCode,
    c.LEFT_EXPRESSION                AS LeftExpression,
    c.OPERATOR                       AS [Operator],
    c.RIGHT_VALUE                    AS RightValue,

    nextRo.SID                       AS NextRouteOperationSidInternal,
    nextRo.SEQ                       AS NextRouteOperationSeq,
    nextOp.OPERATION_CODE            AS NextOperationCode,
    nextOp.OPERATION_NAME            AS NextOperationName,

    extra.SID                        AS ExtraSid,
    extraOp.OPERATION_CODE           AS ExtraOperationCode,
    extraOp.OPERATION_NAME           AS ExtraOperationName
FROM BAS_ROUTE_OPERATION_CONDITION roc
JOIN BAS_CONDITION c 
    ON c.SID = roc.BAS_CONDITION_SID
   AND c.IS_DELETE = 0
LEFT JOIN BAS_ROUTE_OPERATION nextRo 
    ON nextRo.SID = roc.NEXT_ROUTE_OPERATION_SID
   AND nextRo.IS_DELETE = 0
LEFT JOIN BAS_OPERATION nextOp 
    ON nextOp.SID = nextRo.BAS_OPERATION_SID
   AND nextOp.IS_DELETE = 0
LEFT JOIN BAS_ROUTE_OPERATION_EXTRA extra 
    ON extra.SID = roc.NEXT_ROUTE_EXTRA_OPERATION_SID
   AND extra.IS_DELETE = 0
LEFT JOIN BAS_OPERATION extraOp 
    ON extraOp.SID = extra.BAS_OPERATION_SID
   AND extraOp.IS_DELETE = 0
WHERE roc.BAS_ROUTE_OPERATION_SID IN (
    SELECT SID 
    FROM BAS_ROUTE_OPERATION 
    WHERE BAS_ROUTE_SID = @RouteSid
      AND IS_DELETE = 0
)
  AND roc.IS_DELETE = 0
ORDER BY roc.BAS_ROUTE_OPERATION_SID, roc.SEQ;";

            var conditionRows = (await _db.QueryAsync<dynamic>(
                sqlConditions,
                new { RouteSid = routeSid },
                timeoutSeconds: 30,
                ct: ct)).ToList();

            // 5. 將 conditions 塞回對應的 operation
            var condLookup = conditionRows
                .GroupBy(r => (decimal)r.RouteOperationSid)
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var op in operations)
            {
                if (!condLookup.TryGetValue(op.RouteOperationSid, out var rows))
                    continue;

                foreach (var row in rows)
                {
                    var vm = new RouteOperationConditionViewModel
                    {
                        ConditionSid = row.ConditionSid,
                        RouteOperationSid = row.RouteOperationSid,
                        ConditionDefinitionSid = row.ConditionDefinitionSid,
                        Seq = row.Seq,
                        NextRouteOperationSid = row.NextRouteOperationSid,
                        NextRouteExtraOperationSid = row.NextRouteExtraOperationSid,
                        Hold = row.Hold,
                        ConditionCode = row.ConditionCode,
                        LeftExpression = row.LeftExpression,
                        Operator = row.Operator,
                        RightValue = row.RightValue
                    };

                    if (row.NextRouteOperationSidInternal != null)
                    {
                        vm.NextOperation = new NextOperationInfo
                        {
                            RouteOperationSid = row.NextRouteOperationSidInternal,
                            Seq = row.NextRouteOperationSeq ?? 0,
                            OperationCode = row.NextOperationCode,
                            OperationName = row.NextOperationName
                        };
                    }

                    if (row.ExtraSid != null)
                    {
                        vm.NextExtraOperation = new NextExtraOperationInfo
                        {
                            RouteExtraSid = row.ExtraSid,
                            OperationCode = row.ExtraOperationCode,
                            OperationName = row.ExtraOperationName
                        };
                    }

                    op.Conditions.Add(vm);
                }
            }

            result.Operations = operations;
            result.ExtraOperations = extraOperations;

            return result;
        }

        #endregion

        #region RouteOperation CRUD

        public async Task<bool> SeqExistsAsync(decimal routeSid, int seq, CancellationToken ct, decimal? excludeSid = null)
        {
            var where = new WhereBuilder<BAS_ROUTE_OPERATION>()
                .AndEq(x => x.BAS_ROUTE_SID, routeSid)
                .AndEq(x => x.SEQ, seq);

            var list = await _sqlHelper.SelectWhereAsync(where, ct);
            return list.Any(x => !excludeSid.HasValue || x.SID != excludeSid.Value);
        }

        public async Task<decimal> CreateRouteOperationAsync(CreateRouteOperationRequest request, CancellationToken ct)
        {
            var entity = RouteOperationMapper.MapperCreateRouteOperation(request);
            await _sqlHelper.InsertAsync(entity, ct);
            return entity.SID;
        }

        public async Task<RouteOperationDetailViewModel?> GetRouteOperationAsync(decimal routeOperationSid, CancellationToken ct)
        {
            // Join BAS_ROUTE_OPERATION + BAS_OPERATION
            const string sql = @"
SELECT 
    ro.SID              AS RouteOperationSid,
    ro.BAS_ROUTE_SID    AS RouteSid,
    ro.BAS_OPERATION_SID AS OperationSid,
    ro.SEQ,
    ro.ERP_STAGE AS ErpStage,
    ro.END_FLAG AS EndFlag,
    op.OPERATION_CODE AS OperationCode,
    op.OPERATION_NAME AS OperationName
FROM BAS_ROUTE_OPERATION ro
JOIN BAS_OPERATION op ON op.SID = ro.BAS_OPERATION_SID
WHERE ro.IS_DELETE = 0 AND ro.SID = @Sid;";

            var op = await _db.QueryFirstOrDefaultAsync<RouteOperationDetailViewModel>(
                sql,
                new { Sid = routeOperationSid },
                timeoutSeconds: 30,
                ct: ct);

            if (op is null) return null;

            // 附上該站的條件
            const string sqlCond = @"
SELECT 
    roc.SID                         AS ConditionSid,
    roc.BAS_ROUTE_OPERATION_SID     AS RouteOperationSid,
    roc.BAS_CONDITION_SID           AS ConditionDefinitionSid,
    ISNULL(roc.SEQ, 0)             AS Seq,
    roc.NEXT_ROUTE_OPERATION_SID    AS NextRouteOperationSid,
    roc.NEXT_ROUTE_EXTRA_OPERATION_SID AS NextRouteExtraOperationSid,
    roc.HOLD                        AS Hold,
    c.CONDITION_CODE                AS ConditionCode,
    c.LEFT_EXPRESSION               AS LeftExpression,
    c.OPERATOR                      AS [Operator],
    c.RIGHT_VALUE                   AS RightValue
FROM BAS_ROUTE_OPERATION_CONDITION roc
JOIN BAS_CONDITION c ON c.SID = roc.BAS_CONDITION_SID
WHERE roc.BAS_ROUTE_OPERATION_SID = @RouteOperationSid
ORDER BY roc.SEQ;";

            var conds = (await _db.QueryAsync<RouteOperationConditionViewModel>(
                sqlCond,
                new { RouteOperationSid = routeOperationSid },
                timeoutSeconds: 30,
                ct: ct)).ToList();

            op.Conditions = conds;
            return op;
        }

        public async Task UpdateRouteOperationAsync(decimal routeOperationSid, UpdateRouteOperationRequest request, CancellationToken ct)
        {
            var where = new WhereBuilder<BAS_ROUTE_OPERATION>()
                .AndEq(x => x.SID, routeOperationSid);

            var entity = await _sqlHelper.SelectFirstOrDefaultAsync(where, ct)
                         ?? throw new InvalidOperationException($"RouteOperation not found: {routeOperationSid}");

            RouteOperationMapper.MapperUpdateRouteOperation(entity, request);
            await _sqlHelper.UpdateAllByIdAsync(entity, UpdateNullBehavior.IgnoreNulls, true, ct);
        }

        public async Task DeleteRouteOperationAsync(decimal routeOperationSid, CancellationToken ct)
        {
            // 這裡用 DeleteWhereAsync 邏輯可依你專案設定改成軟刪除或實體刪除
            var where = new WhereBuilder<BAS_ROUTE_OPERATION>()
                .AndEq(x => x.SID, routeOperationSid);
            await _sqlHelper.DeleteWhereAsync(where, ct);
        }

        #endregion
        
        #region RouteExtraOperation CRUD
        public async Task<decimal> CreateRouteExtraOperationAsync(CreateRouteExtraOperationRequest request, CancellationToken ct)
        {
            var entity = RouteOperationMapper.MapperCreateRouteExtraOperation(request);
            await _sqlHelper.InsertAsync(entity, ct);
            return entity.SID;
        }
        
        public async Task<RouteExtraOperationDetailViewModel?> GetRouteExtraOperationAsync(decimal routeOperationSid, CancellationToken ct)
        {
            const string sql = @"
SELECT 
    ro.SID              AS RouteOperationSid,
    ro.BAS_ROUTE_SID    AS RouteSid,
    ro.BAS_OPERATION_SID AS OperationSid,
    op.OPERATION_CODE AS OperationCode,
    op.OPERATION_NAME AS OperationName
FROM BAS_ROUTE_OPERATION_EXTRA ro
JOIN BAS_OPERATION op ON op.SID = ro.BAS_OPERATION_SID
WHERE ro.SID = @Sid;";   

            var op = await _db.QueryFirstOrDefaultAsync<RouteExtraOperationDetailViewModel>(
                sql,
                new { Sid = routeOperationSid },
                timeoutSeconds: 30,
                ct: ct);

            if (op is null) return null;

            // Extra 站預設沒有自己的條件清單
            op.Conditions = new List<RouteOperationConditionViewModel>();

            return op;
        }
        
        public async Task DeleteRouteExtraOperationAsync(decimal routeOperationSid, CancellationToken ct)
        {
            // 這裡用 DeleteWhereAsync 邏輯可依你專案設定改成軟刪除或實體刪除
            var where = new WhereBuilder<BAS_ROUTE_OPERATION_EXTRA>()
                .AndEq(x => x.SID, routeOperationSid);
            await _sqlHelper.DeleteWhereAsync(where, ct);
        }
        #endregion

        #region Condition CRUD

        public async Task<bool> ConditionSeqExistsAsync(decimal routeOperationSid, int seq, CancellationToken ct, decimal? excludeSid = null)
        {
            var where = new WhereBuilder<BAS_ROUTE_OPERATION_CONDITION>()
                .AndEq(x => x.BAS_ROUTE_OPERATION_SID, routeOperationSid)
                .AndEq(x => x.SEQ, seq);

            var list = await _sqlHelper.SelectWhereAsync(where, ct);
            return list.Any(x => !excludeSid.HasValue || x.SID != excludeSid.Value);
        }

        public async Task<decimal> CreateConditionAsync(CreateRouteOperationConditionRequest request, CancellationToken ct)
        {
            var entity = RouteOperationMapper.MapperCreateCondition(request);
            await _sqlHelper.InsertAsync(entity, ct);
            return entity.SID;
        }

        public async Task<RouteOperationConditionViewModel?> GetConditionAsync(decimal conditionSid, CancellationToken ct)
        {
            const string sql = @"
SELECT 
    roc.SID                         AS ConditionSid,
    roc.BAS_ROUTE_OPERATION_SID     AS RouteOperationSid,
    roc.BAS_CONDITION_SID           AS ConditionDefinitionSid,
    ISNULL(roc.SEQ, 0)             AS Seq,
    roc.NEXT_ROUTE_OPERATION_SID    AS NextRouteOperationSid,
    roc.NEXT_ROUTE_EXTRA_OPERATION_SID AS NextRouteExtraOperationSid,
    roc.HOLD                        AS Hold,
    c.CONDITION_CODE                AS ConditionCode,
    c.LEFT_EXPRESSION               AS LeftExpression,
    c.OPERATOR                      AS [Operator],
    c.RIGHT_VALUE                   AS RightValue
FROM BAS_ROUTE_OPERATION_CONDITION roc
JOIN BAS_CONDITION c ON c.SID = roc.BAS_CONDITION_SID
WHERE roc.SID = @Sid;";

            var cond = await _db.QueryFirstOrDefaultAsync<RouteOperationConditionViewModel>(
                sql,
                new { Sid = conditionSid },
                timeoutSeconds: 30,
                ct: ct);

            return cond;
        }

        public async Task UpdateConditionAsync(decimal conditionSid, UpdateRouteOperationConditionRequest request, CancellationToken ct)
        {
            var where = new WhereBuilder<BAS_ROUTE_OPERATION_CONDITION>()
                .AndEq(x => x.SID, conditionSid);

            var entity = await _sqlHelper.SelectFirstOrDefaultAsync(where, ct)
                         ?? throw new InvalidOperationException($"Condition not found: {conditionSid}");

            RouteOperationMapper.MapperUpdateCondition(entity, request);
            await _sqlHelper.UpdateAllByIdAsync(entity, UpdateNullBehavior.IgnoreNulls, true, ct);
        }

        public async Task DeleteConditionAsync(decimal conditionSid, CancellationToken ct)
        {
            var where = new WhereBuilder<BAS_ROUTE_OPERATION_CONDITION>()
                .AndEq(x => x.SID, conditionSid);
            await _sqlHelper.DeleteWhereAsync(where, ct);
        }

        #endregion
    }
}

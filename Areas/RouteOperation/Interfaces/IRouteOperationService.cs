using ClassLibrary;
using DcMateH5Api.Areas.Permission.Models;
using DcMateH5Api.Areas.Permission.ViewModels.Menu;
using DcMateH5Api.Areas.Permission.ViewModels.PermissionManagement;
using DcMateH5Api.Areas.RouteOperation.ViewModels;

namespace DcMateH5Api.Areas.RouteOperation.Interfaces
{
    public interface IRouteOperationService
    {
        // Route 組態
        Task<bool> RouteExistsAsync(decimal routeSid, CancellationToken ct);
        Task<RouteConfigViewModel?> GetRouteConfigAsync(decimal routeSid, CancellationToken ct);

        // 調整指定 Route 下主線站別的排序（SEQ）
        Task<bool> ReorderRouteOperationsAsync(decimal routeSid, IReadOnlyList<decimal> orderedRouteOperationSids, CancellationToken ct);
        
        // Operation / Extra master 檢查
        Task<bool> OperationExistsAsync(decimal operationSid, CancellationToken ct);
        Task<bool> ExtraOperationExistsAsync(decimal extraSid, CancellationToken ct);

        // RouteOperation
        Task<bool> RouteOperationExistsAsync(decimal routeOperationSid, CancellationToken ct);
        Task<bool> SeqExistsAsync(decimal routeSid, int seq, CancellationToken ct, decimal? excludeSid = null);
        Task<decimal> CreateRouteOperationAsync(CreateRouteOperationRequest request, CancellationToken ct);
        Task<RouteOperationDetailViewModel?> GetRouteOperationAsync(decimal routeOperationSid, CancellationToken ct);
        Task UpdateRouteOperationAsync(decimal routeOperationSid, UpdateRouteOperationRequest request, CancellationToken ct);
        Task DeleteRouteOperationAsync(decimal routeOperationSid, CancellationToken ct);
        
        // RouteOperationExtra
        Task<decimal> CreateRouteExtraOperationAsync(CreateRouteExtraOperationRequest request, CancellationToken ct);
        Task<RouteExtraOperationDetailViewModel?> GetRouteExtraOperationAsync(decimal routeOperationSid, CancellationToken ct);
        Task DeleteRouteExtraOperationAsync(decimal routeOperationSid, CancellationToken ct);

        
        
        // Condition definition（BAS_CONDITION）檢查
        Task<bool> ConditionDefinitionExistsAsync(decimal conditionSid, CancellationToken ct);

        // RouteOperationCondition
        Task<bool> ConditionSeqExistsAsync(decimal routeOperationSid, int seq, CancellationToken ct, decimal? excludeSid = null);
        Task<decimal> CreateConditionAsync(CreateRouteOperationConditionRequest request, CancellationToken ct);
        Task<RouteOperationConditionViewModel?> GetConditionAsync(decimal conditionSid, CancellationToken ct);
        Task UpdateConditionAsync(decimal conditionSid, UpdateRouteOperationConditionRequest request, CancellationToken ct);
        Task DeleteConditionAsync(decimal conditionSid, CancellationToken ct);
    }

}


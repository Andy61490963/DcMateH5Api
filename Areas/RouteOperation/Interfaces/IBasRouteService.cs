using DcMateH5Api.Areas.RouteOperation.ViewModels;

namespace DcMateH5Api.Areas.RouteOperation.Interfaces;

public interface IBasRouteService
{
    Task<decimal> CreateAsync(CreateRouteRequest request, CancellationToken ct);
    Task<RouteViewModel?> GetAsync(decimal sid, CancellationToken ct);
    Task<IEnumerable<RouteViewModel>> GetAllAsync(CancellationToken ct);
    Task UpdateAsync(decimal sid, UpdateRouteRequest request, CancellationToken ct);
    Task DeleteAsync(decimal sid, CancellationToken ct);
    Task<bool> RouteCodeExistsAsync(string routeCode, CancellationToken ct, decimal? excludeSid = null);
}
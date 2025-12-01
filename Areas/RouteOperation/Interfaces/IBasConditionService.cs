using DcMateH5Api.Areas.RouteOperation.ViewModels;

namespace DcMateH5Api.Areas.RouteOperation.Interfaces;

public interface IBasConditionService
{
    Task<decimal> CreateAsync(CreateConditionRequest request, CancellationToken ct);
    Task<ConditionViewModel?> GetAsync(decimal sid, CancellationToken ct);
    Task<IEnumerable<ConditionViewModel>> GetAllAsync(CancellationToken ct);
    Task UpdateAsync(decimal sid, UpdateConditionRequest request, CancellationToken ct);
    Task DeleteAsync(decimal sid, CancellationToken ct);
    Task<bool> ConditionCodeExistsAsync(string conditionCode, CancellationToken ct, decimal? excludeSid = null);
}
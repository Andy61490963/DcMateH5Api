using DcMateH5Api.Areas.RouteOperation.ViewModels;

namespace DcMateH5Api.Areas.RouteOperation.Interfaces;

public interface IBasOperationService
{
    Task<decimal> CreateAsync(CreateOperationRequest request, CancellationToken ct);
    Task<OperationViewModel?> GetAsync(decimal sid, CancellationToken ct);
    Task<IEnumerable<OperationViewModel>> GetAllAsync(CancellationToken ct);
    Task UpdateAsync(decimal sid, UpdateOperationRequest request, CancellationToken ct);
    Task DeleteAsync(decimal sid, CancellationToken ct);
    Task<bool> OperationCodeExistsAsync(string operationCode, CancellationToken ct, decimal? excludeSid = null);
}
using DcMateH5.Abstractions.Prj.Models;

namespace DcMateH5.Abstractions.Prj;

public interface IPrjService
{
    Task<PagedResult<PrjProjectListItemDto>> GetProjectsAsync(PrjProjectQuery query, CancellationToken ct = default);
    Task<PrjProjectDto> GetProjectAsync(string projectCode, CancellationToken ct = default);
    Task<PrjProjectDto> CreateProjectAsync(CreatePrjProjectRequest request, CancellationToken ct = default);
    Task<PrjProjectDto> UpdateProjectAsync(string projectCode, UpdatePrjProjectRequest request, CancellationToken ct = default);
    Task<PrjProjectDto> ChangeProjectEnabledAsync(string projectCode, ChangeEnabledRequest request, CancellationToken ct = default);
    Task ReorderProjectsAsync(ReorderPrjProjectsRequest request, CancellationToken ct = default);
    Task<PagedResult<PrjDetailDto>> GetDetailsAsync(string projectCode, PrjDetailQuery query, CancellationToken ct = default);
    Task<PrjDetailDto> GetDetailAsync(decimal detailSid, CancellationToken ct = default);
    Task<PrjDetailDto> CreateDetailAsync(string projectCode, CreatePrjDetailRequest request, CancellationToken ct = default);
    Task<PrjDetailDto> UpdateDetailAsync(decimal detailSid, UpdatePrjDetailRequest request, CancellationToken ct = default);
    Task<PrjDetailDto> ChangeDetailStatusAsync(decimal detailSid, ChangePrjDetailStatusRequest request, CancellationToken ct = default);
    Task<PrjDetailDto> ChangeDetailEnabledAsync(decimal detailSid, ChangeEnabledRequest request, CancellationToken ct = default);
    Task ReorderDetailsAsync(string projectCode, ReorderPrjDetailsRequest request, CancellationToken ct = default);
    Task<PrjLookupOptionsDto> GetOptionsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<PrjTextOptionDto>> GetCustomersAsync(string? keyword, int take, CancellationToken ct = default);
    Task<IReadOnlyList<PrjTextOptionDto>> GetUsersAsync(string? keyword, int take, CancellationToken ct = default);
}

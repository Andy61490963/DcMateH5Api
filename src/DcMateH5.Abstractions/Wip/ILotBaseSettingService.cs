using DcMateH5Api.Areas.Wip.Model;
using DcMateH5Api.Models;

namespace DcMateH5.Abstractions.Wip;

public interface ILotBaseSettingService
{
    Task<Result<bool>> CreateLotAsync(WipCreateLotInputDto input, CancellationToken ct = default);
    Task<Result<bool>> CreateLotsAsync(IEnumerable<WipCreateLotInputDto> inputs, CancellationToken ct = default);
    Task<Result<bool>> LotCheckInAsync(WipLotCheckInInputDto input, CancellationToken ct = default);
    Task<Result<bool>> LotCheckInCancelAsync(WipLotCheckInCancelInputDto input, CancellationToken ct = default);
    Task<Result<bool>> LotCheckOutAsync(WipLotCheckOutInputDto input, CancellationToken ct = default);
    Task<Result<bool>> LotReassignOperationAsync(WipLotReassignOperationInputDto input, CancellationToken ct = default);
    Task<Result<bool>> LotRecordDcAsync(WipLotRecordDcInputDto input, CancellationToken ct = default);
    Task<Result<bool>> LotHoldAsync(WipLotHoldInputDto input, CancellationToken ct = default);
    Task<Result<bool>> LotHoldReleaseAsync(WipLotHoldReleaseInputDto input, CancellationToken ct = default);
}

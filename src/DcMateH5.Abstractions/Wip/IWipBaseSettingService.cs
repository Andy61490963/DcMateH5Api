using DcMateH5Api.Areas.Wip.Model;

namespace DcMateH5.Abstractions.Wip;

public interface IWipBaseSettingService
{
    Task<decimal> CheckInAsync(WipCheckInInputDto input, CancellationToken ct = default);
    Task CheckInCancelAsync(WipCheckInCancelInputDto input, CancellationToken ct = default);
    Task AddDetailsAsync(WipAddDetailInputDto input, CancellationToken ct = default);
    Task EditDetailsAsync(WipEditDetailInputDto input, CancellationToken ct = default);
    Task DeleteDetailsAsync(WipDeleteDetailInputDto input, CancellationToken ct = default);
    Task CheckOutAsync(WipCheckOutInputDto input, CancellationToken ct = default);
    
    Task CheckInAddDetailsCheckOutAsync(WipCheckInAddDetailsCheckOutInputDto input, CancellationToken ct = default);
}


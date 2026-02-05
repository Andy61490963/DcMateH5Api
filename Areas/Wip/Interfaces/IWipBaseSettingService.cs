

using DcMateH5Api.Areas.Wip.Model;

namespace DcMateH5Api.Areas.Wip.Interfaces
{
    public interface IWipBaseSettingService
    {
        Task CheckInAsync(WipCheckInInputDto input, CancellationToken ct = default);
        Task AddDetailsAsync(WipAddDetailInputDto input, CancellationToken ct = default);
        Task EditDetailsAsync(WipEditDetailInputDto input, CancellationToken ct = default);
        Task CheckOutAsync(WipCheckOutInputDto input, CancellationToken ct = default);
    }
}

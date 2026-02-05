

using DcMateH5Api.Areas.Wip.Model;

namespace DcMateH5Api.Areas.Wip.Interfaces
{
    public interface IWipBaseSettingService
    {
        Task CheckInAsync(WipCheckInInputDto input, CancellationToken ct = default);
    }
}

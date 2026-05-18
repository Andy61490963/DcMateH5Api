using DcMateH5Api.Areas.Wip.Model;

namespace DcMateH5.Abstractions.Wip;

public interface IWipBaseSettingService
{
    Task<decimal> CheckInAsync(WipCheckInInputDto input, CancellationToken ct = default);

    /// <summary>
    /// 上模開始，建立相關 TOL、HIST 與 CAV 紀錄。
    /// </summary>
    Task<WipModelUploadCheckInResponseDto> ModelUploadCheckInAsync(WipModelUploadCheckInInputDto input, CancellationToken ct = default);

    /// <summary>
    /// 下模結束，關閉相關 HIST 與 CAV 紀錄。
    /// </summary>
    Task ModelUploadCheckOutAsync(WipModelUploadCheckOutInputDto input, CancellationToken ct = default);

    Task CheckInCancelAsync(WipCheckInCancelInputDto input, CancellationToken ct = default);
    Task AddDetailsAsync(WipAddDetailInputDto input, CancellationToken ct = default);
    Task EditDetailsAsync(WipEditDetailInputDto input, CancellationToken ct = default);
    Task DeleteDetailsAsync(WipDeleteDetailInputDto input, CancellationToken ct = default);

    /// <summary>
    /// 更新單筆上模 CAV 數值。
    /// </summary>
    Task EditModelUploadCavAsync(WipEditModelUploadCavInputDto input, CancellationToken ct = default);

    /// <summary>
    /// 更新上模結束時間。
    /// </summary>
    Task EditModelUploadEndAsync(WipEditModelUploadEndInputDto input, CancellationToken ct = default);

    /// <summary>
    /// 更新下模開始時間。
    /// </summary>
    Task EditModelRemoveStartAsync(WipEditModelRemoveStartInputDto input, CancellationToken ct = default);

    Task CheckOutAsync(WipCheckOutInputDto input, CancellationToken ct = default);
    Task AddHistDcAsync(WipOpiWdoeacicoHistDcInputDto input, CancellationToken ct = default);
    Task CheckInAddDetailsCheckOutAsync(WipCheckInAddDetailsCheckOutInputDto input, CancellationToken ct = default);
}

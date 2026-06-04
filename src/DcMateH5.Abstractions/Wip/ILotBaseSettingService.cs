using DcMateH5Api.Areas.Wip.Model;
using DcMateH5Api.Models;

namespace DcMateH5.Abstractions.Wip;

/// <summary>
/// 處理 WIP LOT 生命週期操作。
/// 單筆方法會使用一個交易；批次方法則每筆 LOT 各自使用交易，並回傳逐筆失敗資訊。
/// </summary>
public interface ILotBaseSettingService
{
    /// <summary>
    /// 建立單筆 LOT，並寫入初始 CREATE_LOT 與 OPER_START 歷程。
    /// </summary>
    Task<Result<bool>> CreateLotAsync(WipCreateLotInputDto input, CancellationToken ct = default);

    /// <summary>
    /// 建立多筆 LOT；單筆失敗會收集在結果中，不會 rollback 已成功的 LOT。
    /// </summary>
    Task<Result<bool>> CreateLotsAsync(IEnumerable<WipCreateLotInputDto> inputs, CancellationToken ct = default);

    /// <summary>
    /// 將單筆 LOT 由 Wait 轉為 Run，並建立目前人員與機台紀錄。
    /// </summary>
    Task<Result<bool>> LotCheckInAsync(WipLotCheckInInputDto input, CancellationToken ct = default);

    /// <summary>
    /// 多筆 LOT 進站；單筆失敗會收集在結果中，不會 rollback 已成功的 LOT。
    /// </summary>
    Task<Result<bool>> LotCheckInsAsync(IEnumerable<WipLotCheckInInputDto> inputs, CancellationToken ct = default);

    /// <summary>
    /// 取消有效進站紀錄，並將 LOT 由 Run 還原為 Wait。
    /// </summary>
    Task<Result<bool>> LotCheckInCancelAsync(WipLotCheckInCancelInputDto input, CancellationToken ct = default);

    /// <summary>
    /// 將單筆 LOT 從目前站點出站，並推進 route 狀態。
    /// </summary>
    Task<Result<bool>> LotCheckOutAsync(WipLotCheckOutInputDto input, CancellationToken ct = default);

    /// <summary>
    /// 多筆 LOT 出站；單筆失敗會收集在結果中，不會 rollback 已成功的 LOT。
    /// </summary>
    Task<Result<bool>> LotCheckOutsAsync(IEnumerable<WipLotCheckOutInputDto> inputs, CancellationToken ct = default);

    /// <summary>
    /// 將 LOT 重新分派到目前 route 中不同的站點順序。
    /// </summary>
    Task<Result<bool>> LotReassignOperationAsync(WipLotReassignOperationInputDto input, CancellationToken ct = default);

    /// <summary>
    /// 記錄 LOT 站點的 DC 項目值。
    /// </summary>
    Task<Result<bool>> LotRecordDcAsync(WipLotRecordDcInputDto input, CancellationToken ct = default);

    /// <summary>
    /// 將 LOT Hold，並寫入 Hold 歷程。
    /// </summary>
    Task<Result<bool>> LotHoldAsync(WipLotHoldInputDto input, CancellationToken ct = default);

    /// <summary>
    /// 解除 LOT 的有效 Hold 歷程。
    /// </summary>
    Task<Result<bool>> LotHoldReleaseAsync(WipLotHoldReleaseInputDto input, CancellationToken ct = default);

    /// <summary>
    /// 追加 LOT 數量。
    /// </summary>
    Task<Result<bool>> LotBonusAsync(WipLotBonusInputDto input, CancellationToken ct = default);

    /// <summary>
    /// 報廢 LOT 數量。
    /// </summary>
    Task<Result<bool>> LotScrapAsync(WipLotScrapInputDto input, CancellationToken ct = default);

    /// <summary>
    /// 將 LOT 變更為呼叫端提供的 NEW_STATE_CODE。
    /// </summary>
    Task<Result<bool>> LotStateChangeAsync(WipLotStateChangeInputDto input, CancellationToken ct = default);

    /// <summary>
    /// 固定狀態動作：Wait 或 Hold 轉為 Terminated。
    /// </summary>
    Task<Result<bool>> LotTerminatedAsync(WipLotStatusActionInputDto input, CancellationToken ct = default);

    /// <summary>
    /// 固定狀態動作：Terminated 轉為 Wait。
    /// </summary>
    Task<Result<bool>> LotUnTerminatedAsync(WipLotStatusActionInputDto input, CancellationToken ct = default);

    /// <summary>
    /// 固定狀態動作：Wait 轉為 Finished。
    /// </summary>
    Task<Result<bool>> LotFinishedAsync(WipLotStatusActionInputDto input, CancellationToken ct = default);

    /// <summary>
    /// 固定狀態動作：Finished 轉為 Wait。
    /// </summary>
    Task<Result<bool>> LotUnFinishedAsync(WipLotStatusActionInputDto input, CancellationToken ct = default);
}

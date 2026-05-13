using System.Data;
using DbExtensions;
using DcMateClassLibrary.Helper;
using DcMateClassLibrary.Helper.HttpHelper;
using DcMateH5.Abstractions.Wip;
using DcMateH5Api.Areas.Wip.Model;
using DcMateH5Api.Models;
using Microsoft.Data.SqlClient;

namespace DcMateH5.Infrastructure.Wip;

public class LotBaseSettingService : ILotBaseSettingService
{
    private static class Flags
    {
        public const string No = "N";
        public const string LotTypeNormal = "N";
        public const string OperationFinishNo = "N";
    }

    private static class CreateLotActionCodes
    {
        public const string CreateLot = "CREATE_LOT";
        public const string OperationStart = "OPER_START";
        public const string CreatedStatus = "Created";
        public const string WaitStatus = "Wait";
    }

    private static class LotCheckInCodes
    {
        public const string CheckIn = "CHECK_IN";
        public const string WaitStatus = "Wait";
        public const string RunStatus = "Run";
        public const string ControlModeOne = "ONE";
    }

    private static class LotCheckInCancelCodes
    {
        public const string CheckInCancel = "CHECK_IN_CANCEL";
        public const string WaitStatus = "Wait";
    }

    private static class LotCheckOutCodes
    {
        public const string CheckOut = "CHECK_OUT";
        public const string OperEnd = "OPER_END";
        public const string OperStart = "OPER_START";
        public const string EndLot = "END_LOT";
        public const string WaitStatus = "Wait";
        public const string FinishedStatus = "Finished";
        public const string ControlModeOne = "ONE";
        public const string ControlModeGroup = "GROUP";
    }

    private static class LotHoldCodes
    {
        public const string Hold = "LOT_HOLD";
        public const string HoldRelease = "LOT_HOLD_RELEASE";
        public const string HoldStatus = "Hold";
        public static readonly string[] AllowHoldStatuses = ["Wait", "Run"];
    }

    private static class LotReassignOperationCodes
    {
        public const string ReassignOperation = "LOT_RESSIGN_OPER";
        public const string WaitStatus = "Wait";
        public const string OperEnd = "OPER_END";
        public const string OperStart = "OPER_START";
    }

    private static class LotRecordDcCodes
    {
        public const string DefaultActionCode = "LOT_RECORD_DC";
    }

    private static class LotQuantityAdjustCodes
    {
        public const string BonusAction = "LOT_BONUS";
        public const string ScrapAction = "LOT_NG";
        public const string BonusReasonType = "BONUS";
        public const string ScrapReasonType = "NG";
    }

    private static class LotStateChangeCodes
    {
        public const string StateChangeAction = "LOT_STATE_CHANGE";
        public const string TerminatedAction = "LOT_TERMINATED";
        public const string UnTerminatedAction = "LOT_UNTERMINATED";
        public const string FinishedAction = "LOT_FINISHED";
        public const string UnFinishedAction = "LOT_UNFINISHED";
        public const string NormalReasonType = "NORMAL";
        public const string WaitStatus = "Wait";
        public const string TerminatedStatus = "Terminated";
        public const string FinishedStatus = "Finished";
    }

    private readonly SQLGenerateHelper _sqlHelper;
    private readonly ISelectDtoService _selectDtoService;

    public LotBaseSettingService(
        SQLGenerateHelper sqlHelper,
        ISelectDtoService selectDtoService)
    {
        _sqlHelper = sqlHelper;
        _selectDtoService = selectDtoService;
    }

    public async Task<Result<bool>> CreateLotAsync(WipCreateLotInputDto input, CancellationToken ct = default)
    {
        await _sqlHelper.TxAsync(
            async (conn, tx, innerCt) =>
            {
                await CreateLotInTxAsync(conn, tx, input, innerCt);
            },
            isolation: IsolationLevel.ReadCommitted,
            ct: ct);

        return Result<bool>.Ok(true);
    }

    public async Task<Result<bool>> CreateLotsAsync(IEnumerable<WipCreateLotInputDto> inputs, CancellationToken ct = default)
    {
        var createLotInputs = inputs?.ToList()
                              ?? throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, "CreateLot inputs are required.");

        if (createLotInputs.Count == 0)
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, "CreateLot inputs are required.");

        await _sqlHelper.TxAsync(
            async (conn, tx, innerCt) =>
            {
                foreach (var input in createLotInputs)
                {
                    await CreateLotInTxAsync(conn, tx, input, innerCt);
                }
            },
            isolation: IsolationLevel.ReadCommitted,
            ct: ct);

        return Result<bool>.Ok(true);
    }

    public async Task<Result<bool>> LotCheckInAsync(WipLotCheckInInputDto input, CancellationToken ct = default)
    {
        await _sqlHelper.TxAsync(
            async (conn, tx, innerCt) =>
            {
                await LotCheckInInTxAsync(conn, tx, input, innerCt);
            },
            isolation: IsolationLevel.ReadCommitted,
            ct: ct);

        return Result<bool>.Ok(true);
    }

    public async Task<Result<bool>> LotCheckInCancelAsync(WipLotCheckInCancelInputDto input, CancellationToken ct = default)
    {
        await _sqlHelper.TxAsync(
            async (conn, tx, innerCt) =>
            {
                await LotCheckInCancelInTxAsync(conn, tx, input, innerCt);
            },
            isolation: IsolationLevel.ReadCommitted,
            ct: ct);

        return Result<bool>.Ok(true);
    }

    public async Task<Result<bool>> LotCheckOutAsync(WipLotCheckOutInputDto input, CancellationToken ct = default)
    {
        await _sqlHelper.TxAsync(
            async (conn, tx, innerCt) =>
            {
                await LotCheckOutInTxAsync(conn, tx, input, innerCt);
            },
            isolation: IsolationLevel.ReadCommitted,
            ct: ct);

        return Result<bool>.Ok(true);
    }

    public async Task<Result<bool>> LotReassignOperationAsync(WipLotReassignOperationInputDto input, CancellationToken ct = default)
    {
        await _sqlHelper.TxAsync(
            async (conn, tx, innerCt) =>
            {
                await LotReassignOperationInTxAsync(conn, tx, input, innerCt);
            },
            isolation: IsolationLevel.ReadCommitted,
            ct: ct);

        return Result<bool>.Ok(true);
    }

    public async Task<Result<bool>> LotRecordDcAsync(WipLotRecordDcInputDto input, CancellationToken ct = default)
    {
        await _sqlHelper.TxAsync(
            async (conn, tx, innerCt) =>
            {
                await LotRecordDcInTxAsync(conn, tx, input, innerCt);
            },
            isolation: IsolationLevel.ReadCommitted,
            ct: ct);

        return Result<bool>.Ok(true);
    }

    public async Task<Result<bool>> LotHoldAsync(WipLotHoldInputDto input, CancellationToken ct = default)
    {
        await _sqlHelper.TxAsync(
            async (conn, tx, innerCt) =>
            {
                await LotHoldInTxAsync(conn, tx, input, innerCt);
            },
            isolation: IsolationLevel.ReadCommitted,
            ct: ct);

        return Result<bool>.Ok(true);
    }

    public async Task<Result<bool>> LotHoldReleaseAsync(WipLotHoldReleaseInputDto input, CancellationToken ct = default)
    {
        await _sqlHelper.TxAsync(
            async (conn, tx, innerCt) =>
            {
                await LotHoldReleaseInTxAsync(conn, tx, input, innerCt);
            },
            isolation: IsolationLevel.ReadCommitted,
            ct: ct);

        return Result<bool>.Ok(true);
    }

    public async Task<Result<bool>> LotBonusAsync(WipLotBonusInputDto input, CancellationToken ct = default)
    {
        await _sqlHelper.TxAsync(
            async (conn, tx, innerCt) =>
            {
                await LotBonusInTxAsync(conn, tx, input, innerCt);
            },
            isolation: IsolationLevel.ReadCommitted,
            ct: ct);

        return Result<bool>.Ok(true);
    }

    public async Task<Result<bool>> LotScrapAsync(WipLotScrapInputDto input, CancellationToken ct = default)
    {
        await _sqlHelper.TxAsync(
            async (conn, tx, innerCt) =>
            {
                await LotScrapInTxAsync(conn, tx, input, innerCt);
            },
            isolation: IsolationLevel.ReadCommitted,
            ct: ct);

        return Result<bool>.Ok(true);
    }

    public async Task<Result<bool>> LotStateChangeAsync(WipLotStateChangeInputDto input, CancellationToken ct = default)
    {
        await _sqlHelper.TxAsync(
            async (conn, tx, innerCt) =>
            {
                await LotStateChangeInTxAsync(
                    conn,
                    tx,
                    input,
                    LotStateChangeCodes.StateChangeAction,
                    input.NEW_STATE_CODE,
                    allowedCurrentStatusCodes: null,
                    innerCt);
            },
            isolation: IsolationLevel.ReadCommitted,
            ct: ct);

        return Result<bool>.Ok(true);
    }

    public async Task<Result<bool>> LotTerminatedAsync(WipLotStatusActionInputDto input, CancellationToken ct = default)
    {
        await ExecuteLotStatusActionAsync(
            input,
            LotStateChangeCodes.TerminatedAction,
            LotStateChangeCodes.TerminatedStatus,
            [LotStateChangeCodes.WaitStatus, LotHoldCodes.HoldStatus],
            ct);

        return Result<bool>.Ok(true);
    }

    public async Task<Result<bool>> LotUnTerminatedAsync(WipLotStatusActionInputDto input, CancellationToken ct = default)
    {
        await ExecuteLotStatusActionAsync(
            input,
            LotStateChangeCodes.UnTerminatedAction,
            LotStateChangeCodes.WaitStatus,
            [LotStateChangeCodes.TerminatedStatus],
            ct);

        return Result<bool>.Ok(true);
    }

    public async Task<Result<bool>> LotFinishedAsync(WipLotStatusActionInputDto input, CancellationToken ct = default)
    {
        await ExecuteLotStatusActionAsync(
            input,
            LotStateChangeCodes.FinishedAction,
            LotStateChangeCodes.FinishedStatus,
            [LotStateChangeCodes.WaitStatus],
            ct);

        return Result<bool>.Ok(true);
    }

    public async Task<Result<bool>> LotUnFinishedAsync(WipLotStatusActionInputDto input, CancellationToken ct = default)
    {
        await ExecuteLotStatusActionAsync(
            input,
            LotStateChangeCodes.UnFinishedAction,
            LotStateChangeCodes.WaitStatus,
            [LotStateChangeCodes.FinishedStatus],
            ct);

        return Result<bool>.Ok(true);
    }

    private Task ExecuteLotStatusActionAsync(
        WipLotStatusActionInputDto input,
        string actionCode,
        string targetStatusCode,
        string[] allowedCurrentStatusCodes,
        CancellationToken ct)
    {
        var stateChangeInput = new WipLotStateChangeInputDto
        {
            LOT = input.LOT,
            NEW_STATE_CODE = targetStatusCode,
            REASON_SID = input.REASON_SID,
            DATA_LINK_SID = input.DATA_LINK_SID,
            REPORT_TIME = input.REPORT_TIME,
            ACCOUNT_NO = input.ACCOUNT_NO,
            COMMENT = input.COMMENT,
            INPUT_FORM_NAME = input.INPUT_FORM_NAME
        };

        return _sqlHelper.TxAsync(
            async (conn, tx, innerCt) =>
            {
                await LotStateChangeInTxAsync(
                    conn,
                    tx,
                    stateChangeInput,
                    actionCode,
                    targetStatusCode,
                    allowedCurrentStatusCodes,
                    innerCt);
            },
            isolation: IsolationLevel.ReadCommitted,
            ct: ct);
    }

    /// <summary>
    /// 在單一交易內建立 LOT 主檔與對應歷程，並同步更新工單釋放數量。
    /// 流程包含：
    /// 1. 輸入驗證
    /// 2. LOT 重複檢查
    /// 3. 載入建 LOT 所需主檔資料（工單 / 途程 / 站點 / 狀態）
    /// 4. 建立 LOT 主檔
    /// 5. 建立兩筆歷程（CREATE_LOT、OPER_START）
    /// 6. 更新工單 RELEASE_QTY
    /// </summary>
    private async Task CreateLotInTxAsync(
        SqlConnection conn,
        SqlTransaction tx,
        WipCreateLotInputDto input,
        CancellationToken ct)
    {
        // 先檢查 CancellationToken，避免呼叫端已取消但方法仍繼續執行，
        // 導致不必要的 DB 查詢與交易資源消耗。
        ct.ThrowIfCancellationRequested();

        // ===== 1. 基本輸入驗證 =====
        // 這邊先做必要欄位檢查，目的有兩個：
        // 1. 盡早失敗（Fail Fast），避免進交易後才發現資料不完整。
        // 2. 回傳明確錯誤訊息給前端/呼叫端，而不是讓 DB 或後續流程拋出模糊例外。
        if (string.IsNullOrWhiteSpace(input.LOT))
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, "LOT is required.");

        if (string.IsNullOrWhiteSpace(input.WO))
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, "WO is required.");

        if (string.IsNullOrWhiteSpace(input.ACCOUNT_NO))
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, "ACCOUNT_NO is required.");

        // LOT_QTY 必須大於 0，避免建立出無意義或非法數量的 LOT。
        if (input.LOT_QTY <= 0)
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, "LOT_QTY must be greater than 0.");

        // ===== 2. 檢查 LOT 是否已存在 =====
        // 同一個 LOT 不允許重複建立，否則會造成主檔重複、歷程混亂，
        // 後續追蹤站點、工單數量、報工資料都會出問題。
        // 這裡在交易內檢查，可確保本次交易看到的資料一致性較高。
        await EnsureLotNotExistsInTxAsync(conn, tx, input.LOT, ct);

        // ===== 3. 蒐集建 LOT 所需的基礎資料 =====
        // 建立 LOT 不只是塞一筆主檔而已，還需要：
        // - 建立人/操作人資訊（班別、群組等）
        // - 工單資訊（料號、工廠、工單主鍵）
        // - 製程途程與起始站點
        // - 站點主檔資訊
        // - 料號主檔資訊
        // - LOT 狀態資訊（Created / Wait）
        //
        // 這些資料如果任何一筆缺失，都代表目前系統設定不完整，
        // 不應該硬建 LOT，因此各方法內部都會做 null 檢查並拋出明確錯誤。
        var user = await GetUserByAccountAsync(input.ACCOUNT_NO, ct);
        var workOrder = await GetWorkOrderAsync(input.WO, ct);
        var route = await GetRouteAsync(input.ROUTE_SID, ct);
        var routeOperation = await GetCreateLotRouteOperationAsync(input.ROUTE_SID, input.OPERATION_SID, ct);
        var operation = await GetOperationBySidAsync(routeOperation.WIP_OPERATION_SID, ct);
        var part = await GetPartByPartNoAsync(workOrder.PART_NO!, ct);
        var createdStatus = await GetLotStatusAsync(CreateLotActionCodes.CreatedStatus, ct);
        var waitStatus = await GetLotStatusAsync(CreateLotActionCodes.WaitStatus, ct);

        // ===== 4. 產生系統時間與主鍵 =====
        // 時間使用 DB 端的 SYSDATETIME()，而不是應用程式時間，
        // 這樣可避免：
        // - 多台 API Server 時間不一致
        // - 應用程式主機時間被調整
        // - 歷程排序與 DB 寫入時間有偏差
        var now = await GetDbNowInTxAsync(conn, tx, ct);

        // 這些 SID 為本次建立 LOT 所需的唯一識別值：
        // - operationLinkSid：用來串接同一站點操作的歷程關聯
        // - lotSid：LOT 主檔主鍵
        // - createHistSid：建立 LOT 的第一筆歷程主鍵
        // - operStartHistSid：起始站點待生產的第二筆歷程主鍵
        var operationLinkSid = RandomHelper.GenerateRandomDecimal();
        var lotSid = RandomHelper.GenerateRandomDecimal();
        var createHistSid = RandomHelper.GenerateRandomDecimal();
        var operStartHistSid = RandomHelper.GenerateRandomDecimal();

        // ===== 5. 組裝 WIP_LOT 主檔資料 =====
        // 這筆資料代表正式建立出的 LOT 主檔。
        //
        // 幾個關鍵欄位語意：
        // - LOT_STATUS_*：主檔建立後直接落在 Wait，表示已建檔且等待進站/生產
        // - CUR_OPER_BATCH_ID / ALL_OPER_BATCH_ID：初始化以 lotSid 作為批次識別
        // - PARENT_LOT_*：初始 LOT 自己就是自己的 parent
        // - OPERATION_*：目前站點即為 Route 的第一站
        // - CUR_OPERATION_LINK_SID：串聯當前站點的操作歷程
        // - CUR_OPER_FIRST_IN_FLAG：初始為 N，代表尚未有首投入站行為
        // - NG / OUT 數量：新建 LOT 時皆為 0
        var lot = new WipLotDto
        {
            LOT_SID = lotSid,
            LOT = input.LOT,
            CUR_OPER_BATCH_ID = lotSid,
            ALL_OPER_BATCH_ID = lotSid,
            ALIAS_LOT1 = input.ALIAS_LOT1,
            ALIAS_LOT2 = input.ALIAS_LOT2,
            LOT_TYPE = Flags.LotTypeNormal,
            PARENT_LOT_SID = lotSid.ToString(),
            PARENT_LOT = input.LOT,
            CREATE_USER = input.ACCOUNT_NO,
            CREATE_TIME = now,
            EDIT_USER = input.ACCOUNT_NO,
            EDIT_TIME = now,
            LOT_STATUS_SID = waitStatus.LOT_STATUS_SID,
            LOT_STATUS_CODE = waitStatus.LOT_STATUS_CODE,
            WO_SID = workOrder.WO_SID,
            WO = workOrder.WO,
            CUR_RULE_CODE = string.Empty,
            OPERATION_SID = operation.WIP_OPERATION_SID,
            OPERATION_SEQ = routeOperation.SEQ,
            PART_SID = part.WIP_PARTNO_SID,
            PART_NO = part.WIP_PARTNO_NO,
            LOT_QTY = input.LOT_QTY,
            NG_QTY = 0,
            CUR_OPER_OUT_QTY = 0,
            CUR_OPER_NG_OUT_QTY = 0,
            CUR_OPERATION_LINK_SID = operationLinkSid.ToString(),
            FACTORY_SID = workOrder.FACTORY_SID ?? 0,
            COMMENT = input.COMMENT ?? string.Empty,
            LAST_STATUS_CHANGE_TIME = now,
            ROUTE_SID = route.WIP_ROUTE_SID,
            ROUTE_OPER_SID = routeOperation.WIP_ROUTE_OPERATION_SID,
            CUR_OPER_FIRST_IN_FLAG = Flags.No
        };

        // ===== 6. 建立第一筆歷程：CREATE_LOT =====
        // 這筆歷程用來記錄「LOT 被建立」這個事件。
        //
        // 雖然主檔最終狀態是 Wait，
        // 但第一筆歷程仍保留 Created -> Created 的建立語意，
        // 讓系統可以清楚辨識：
        // - LOT 何時建立
        // - 由誰建立
        // - 建立當下關聯哪張工單、哪條 Route、哪個站點
        var createHist = BuildCreateLotHistory(
            createHistSid,
            1,
            input,
            lot,
            createdStatus,
            createdStatus,
            operationLinkSid,
            operation,
            routeOperation,
            workOrder,
            user,
            now);

        // ===== 7. 建立第二筆歷程：OPER_START / Wait =====
        // 第二筆歷程代表 LOT 建立後，立即進入起始工站的待生產狀態。
        //
        // 這樣做的好處是：
        // - 主檔狀態與歷程狀態一致
        // - 歷程上能明確看出先「建立」、再「進入起始站待生產」
        // - 保持與舊系統既有資料語意一致，避免報表或追蹤邏輯失真
        var operStartHist = BuildCreateLotHistory(
            operStartHistSid,
            2,
            input,
            lot,
            waitStatus,
            createdStatus,
            operationLinkSid,
            operation,
            routeOperation,
            workOrder,
            user,
            now);

        // BuildCreateLotHistory 預設 ACTION_CODE 是 CREATE_LOT，
        // 第二筆需改成 OPER_START，代表站點啟動/進入待生產。
        operStartHist.ACTION_CODE = CreateLotActionCodes.OperationStart;

        // ===== 8. 寫入 LOT 主檔與歷程 =====
        // 這兩張表沒有 IS_DELETE 欄位，
        // 但 _sqlHelper 可能會自動補寫審計/預設欄位，
        // 所以這裡暫時關掉 EnableAuditColumns，避免插入不存在欄位造成失敗。
        //
        // 注意：這是 shared state，因此一定要用 try/finally 還原，
        // 避免影響同一個 service 後續其他 DB 操作。
        var originalEnableAuditColumns = _sqlHelper.EnableAuditColumns;
        _sqlHelper.EnableAuditColumns = false;
        try
        {
            // 寫入順序固定為：
            // 1. 主檔 WIP_LOT
            // 2. 建立歷程 CREATE_LOT
            // 3. 起始站歷程 OPER_START
            //
            // 這樣比較符合資料語意，也能讓後續查詢歷程時，
            // 依序看到完整的建立過程。
            await _sqlHelper.InsertInTxAsync(conn, tx, lot, ct: ct);
            await _sqlHelper.InsertInTxAsync(conn, tx, createHist, ct: ct);
            await _sqlHelper.InsertInTxAsync(conn, tx, operStartHist, ct: ct);
        }
        finally
        {
            // 無論成功或失敗都要還原 helper 狀態，
            // 避免影響同一個 service instance 之後的其他操作。
            _sqlHelper.EnableAuditColumns = originalEnableAuditColumns;
        }

        // ===== 9. 回寫工單已釋放數量 =====
        // 建立 LOT 後，代表工單已有一部分數量被釋放到現場生產流程，
        // 因此需要同步更新工單的 RELEASE_QTY。
        //
        // 這一步放在同一個交易裡，確保：
        // - LOT 建立成功但工單數量沒更新，不會發生
        // - 工單數量更新了但 LOT 沒建立成功，也不會發生
        //
        // 這樣可以維持 LOT 與工單統計的一致性。
        await _sqlHelper.UpdateById<WipWoDto>(workOrder.WO_SID)
            .Set(x => x.RELEASE_QTY, (workOrder.RELEASE_QTY ?? 0) + input.LOT_QTY)
            .ExecuteInTxAsync(conn, tx, ct: ct);
    }

    /// <summary>
    /// LOT 進站。
    /// 主要流程與舊版 LotCheckIn 對齊：
    /// 1. 檢查輸入與 LOT 狀態
    /// 2. 寫入 WIP_LOT_HIST 的 CHECK_IN 歷程
    /// 3. 寫入人員目前/歷程資料
    /// 4. 若有設備則寫入設備目前/歷程資料
    /// 5. 將 WIP_LOT 狀態由 Wait 更新為 Run
    /// </summary>
    private async Task LotCheckInInTxAsync(
        SqlConnection conn,
        SqlTransaction tx,
        WipLotCheckInInputDto input,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(input.LOT))
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, "LOT is required.");

        if (string.IsNullOrWhiteSpace(input.ACCOUNT_NO))
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, "ACCOUNT_NO is required.");

        if (input.DATA_LINK_SID <= 0)
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, "DATA_LINK_SID must be greater than 0.");

        var lot = await GetLotByCodeInTxAsync(conn, tx, input.LOT, ct);
        if (!string.Equals(lot.LOT_STATUS_CODE, LotCheckInCodes.WaitStatus, StringComparison.OrdinalIgnoreCase))
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, $"LOT status must be {LotCheckInCodes.WaitStatus}: {input.LOT}");

        var user = await GetUserByAccountAsync(input.ACCOUNT_NO, ct);
        var operation = await GetOperationBySidAsync(lot.OPERATION_SID, ct);
        var runStatus = await GetLotStatusAsync(LotCheckInCodes.RunStatus, ct);
        var reportTime = input.REPORT_TIME ?? await GetDbNowInTxAsync(conn, tx, ct);
        var createTime = await GetDbNowInTxAsync(conn, tx, ct);
        var operationLinkSid = ParseOperationLinkSid(lot.CUR_OPERATION_LINK_SID, lot.LOT);
        var shiftSid = input.SHIFT_SID ?? user.SHIFT_SID ?? 0;
        var workgroupSid = user.WORKGROUP_SID ?? 0;
        var isFirstCheckIn = !string.Equals(lot.CUR_OPER_FIRST_IN_FLAG, "Y", StringComparison.OrdinalIgnoreCase);

        var checkInHist = new WipLotHistDto
        {
            WIP_LOT_HIST_SID = RandomHelper.GenerateRandomDecimal(),
            SEQ = await GetNextLotHistorySequenceInTxAsync(conn, tx, lot.LOT_SID, ct),
            DATA_LINK_SID = input.DATA_LINK_SID,
            LOT_SID = lot.LOT_SID,
            LOT = lot.LOT,
            ALIAS_LOT1 = lot.ALIAS_LOT1,
            ALIAS_LOT2 = lot.ALIAS_LOT2,
            LOT_STATUS_SID = runStatus.LOT_STATUS_SID,
            LOT_STATUS_CODE = runStatus.LOT_STATUS_CODE,
            PRE_LOT_STATUS_SID = lot.LOT_STATUS_SID,
            PRE_LOT_STATUS_CODE = lot.LOT_STATUS_CODE,
            WO_SID = lot.WO_SID,
            WO = lot.WO,
            OPERATION_LINK_SID = operationLinkSid,
            OPERATION_SID = lot.OPERATION_SID,
            OPERATION_CODE = operation.WIP_OPERATION_NO,
            OPERATION_NAME = operation.WIP_OPERATION_NAME ?? operation.WIP_OPERATION_NO,
            OPERATION_SEQ = lot.OPERATION_SEQ,
            OPERATION_FINISH = Flags.OperationFinishNo,
            PART_SID = lot.PART_SID,
            PART_NO = lot.PART_NO,
            LOT_QTY = lot.LOT_QTY,
            TOTAL_OK_QTY = 0,
            TOTAL_NG_QTY = 0,
            TOTAL_DEFECT_QTY = 0,
            TOTAL_USER_COUNT = 1,
            FACTORY_SID = lot.FACTORY_SID.ToString(),
            ACTION_CODE = LotCheckInCodes.CheckIn,
            CONTROL_MODE = LotCheckInCodes.ControlModeOne,
            INPUT_FORM_NAME = input.INPUT_FORM_NAME,
            CREATE_USER = input.ACCOUNT_NO,
            CREATE_TIME = createTime,
            REPORT_TIME = reportTime,
            PRE_REPORT_TIME = reportTime,
            PRE_STATUS_CHANGE_TIME = lot.LAST_STATUS_CHANGE_TIME,
            LOT_QTY1 = lot.LOT_QTY1,
            LOT_QTY2 = lot.LOT_QTY2,
            LOCATION = lot.LOCATION,
            ROUTE_SID = lot.ROUTE_SID,
            OPER_FIRST_CHECK_IN_TIME = isFirstCheckIn ? reportTime : null,
            SHIFT_SID = shiftSid,
            WORKGROUP_SID = workgroupSid,
            LOT_SUB_STATUS_CODE = input.LOT_SUB_STATUS_CODE,
            COMMENT = input.COMMENT ?? string.Empty
        };

        var originalEnableAuditColumns = _sqlHelper.EnableAuditColumns;
        _sqlHelper.EnableAuditColumns = false;
        try
        {
            await _sqlHelper.InsertInTxAsync(conn, tx, checkInHist, ct: ct);
            await InsertLotCheckInUserRecordsInTxAsync(conn, tx, input, lot, checkInHist, operationLinkSid, createTime, shiftSid, workgroupSid, ct);

            if (!string.IsNullOrWhiteSpace(input.EQP_NO))
            {
                var equipment = await GetEquipmentByNoAsync(input.EQP_NO, ct);
                await InsertLotCheckInEquipmentRecordsInTxAsync(conn, tx, input, lot, checkInHist, equipment, operationLinkSid, reportTime, ct);
            }
        }
        finally
        {
            _sqlHelper.EnableAuditColumns = originalEnableAuditColumns;
        }

        await UpdateLotCheckInStateInTxAsync(conn, tx, input, lot, runStatus, reportTime, createTime, isFirstCheckIn, ct);
    }

    private async Task LotCheckInCancelInTxAsync(
        SqlConnection conn,
        SqlTransaction tx,
        WipLotCheckInCancelInputDto input,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(input.LOT))
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, "LOT is required.");

        if (string.IsNullOrWhiteSpace(input.ACCOUNT_NO))
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, "ACCOUNT_NO is required.");

        if (input.DATA_LINK_SID <= 0)
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, "DATA_LINK_SID must be greater than 0.");

        var lot = await GetLotByCodeInTxAsync(conn, tx, input.LOT, ct);
        if (!string.Equals(lot.LOT_STATUS_CODE, LotCheckInCodes.RunStatus, StringComparison.OrdinalIgnoreCase))
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, $"LOT status must be {LotCheckInCodes.RunStatus}: {input.LOT}");

        var user = await GetUserByAccountAsync(input.ACCOUNT_NO, ct);
        var operation = await GetOperationBySidAsync(lot.OPERATION_SID, ct);
        var waitStatus = await GetLotStatusAsync(LotCheckInCancelCodes.WaitStatus, ct);
        var reportTime = input.REPORT_TIME ?? await GetDbNowInTxAsync(conn, tx, ct);
        var createTime = await GetDbNowInTxAsync(conn, tx, ct);
        var operationLinkSid = ParseOperationLinkSid(lot.CUR_OPERATION_LINK_SID, lot.LOT);
        var shiftSid = user.SHIFT_SID ?? 0;
        var workgroupSid = user.WORKGROUP_SID ?? 0;
        var activeUserHistories = await GetActiveLotUserHistoriesInTxAsync(conn, tx, lot.LOT_SID, operationLinkSid, ct);
        if (activeUserHistories.Count == 0)
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, $"No active lot check-in record found: {lot.LOT}");

        var cancelHist = BuildLotHistoryRecord(
            histSid: RandomHelper.GenerateRandomDecimal(),
            seq: await GetNextLotHistorySequenceInTxAsync(conn, tx, lot.LOT_SID, ct),
            dataLinkSid: input.DATA_LINK_SID,
            lot: lot,
            currentStatus: waitStatus,
            previousStatus: new WipLotStatusDto { LOT_STATUS_SID = lot.LOT_STATUS_SID, LOT_STATUS_CODE = lot.LOT_STATUS_CODE },
            operationLinkSid: operationLinkSid,
            operationSid: lot.OPERATION_SID,
            operationCode: operation.WIP_OPERATION_NO,
            operationName: operation.WIP_OPERATION_NAME ?? operation.WIP_OPERATION_NO,
            operationSeq: lot.OPERATION_SEQ,
            operationFinish: Flags.OperationFinishNo,
            totalOkQty: lot.LOT_QTY,
            totalNgQty: 0,
            totalDefectQty: 0,
            totalUserCount: activeUserHistories.Count,
            routeSid: lot.ROUTE_SID,
            factorySid: lot.FACTORY_SID,
            actionCode: LotCheckInCancelCodes.CheckInCancel,
            controlMode: LotCheckOutCodes.ControlModeOne,
            inputFormName: input.INPUT_FORM_NAME,
            createUser: input.ACCOUNT_NO,
            createTime: createTime,
            reportTime: reportTime,
            preReportTime: reportTime,
            preStatusChangeTime: lot.LAST_STATUS_CHANGE_TIME,
            lotQty1: lot.LOT_QTY1,
            lotQty2: lot.LOT_QTY2,
            location: lot.LOCATION,
            operFirstCheckInTime: null,
            shiftSid: shiftSid,
            workgroupSid: workgroupSid,
            lotSubStatusCode: string.Empty,
            comment: input.COMMENT ?? string.Empty);

        var originalEnableAuditColumns = _sqlHelper.EnableAuditColumns;
        _sqlHelper.EnableAuditColumns = false;
        try
        {
            await _sqlHelper.InsertInTxAsync(conn, tx, cancelHist, ct: ct);
        }
        finally
        {
            _sqlHelper.EnableAuditColumns = originalEnableAuditColumns;
        }

        await DeleteCurrentLotEquipmentInTxAsync(conn, tx, lot.LOT_SID, ct);
        await DeleteCurrentLotUsersInTxAsync(conn, tx, lot.LOT_SID, ct);
        await CloseLotUserHistoriesInTxAsync(
            conn,
            tx,
            activeUserHistories,
            cancelHist.WIP_LOT_HIST_SID,
            createTime,
            reportTime,
            input.ACCOUNT_NO,
            shiftSid,
            workgroupSid,
            0,
            0,
            Flags.OperationFinishNo,
            string.Empty,
            ct);
        await UpdateLotCheckInCancelStateInTxAsync(conn, tx, lot, waitStatus, input.ACCOUNT_NO, createTime, reportTime, ct);
    }

    private async Task LotCheckOutInTxAsync(
        SqlConnection conn,
        SqlTransaction tx,
        WipLotCheckOutInputDto input,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(input.LOT))
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, "LOT is required.");

        if (string.IsNullOrWhiteSpace(input.ACCOUNT_NO))
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, "ACCOUNT_NO is required.");

        if (input.DATA_LINK_SID <= 0)
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, "DATA_LINK_SID must be greater than 0.");

        var lot = await GetLotByCodeInTxAsync(conn, tx, input.LOT, ct);
        if (!string.Equals(lot.LOT_STATUS_CODE, LotCheckInCodes.RunStatus, StringComparison.OrdinalIgnoreCase))
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, $"LOT status must be {LotCheckInCodes.RunStatus}: {input.LOT}");

        var user = await GetUserByAccountAsync(input.ACCOUNT_NO, ct);
        var operation = await GetOperationBySidAsync(lot.OPERATION_SID, ct);
        var reportTime = input.REPORT_TIME ?? await GetDbNowInTxAsync(conn, tx, ct);
        var createTime = await GetDbNowInTxAsync(conn, tx, ct);
        var operationLinkSid = ParseOperationLinkSid(lot.CUR_OPERATION_LINK_SID, lot.LOT);
        var shiftSid = input.SHIFT_SID ?? user.SHIFT_SID ?? 0;
        var workgroupSid = user.WORKGROUP_SID ?? 0;
        var activeUserHistories = await GetActiveLotUserHistoriesInTxAsync(conn, tx, lot.LOT_SID, operationLinkSid, ct);
        if (activeUserHistories.Count == 0)
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, $"No active lot check-in record found: {lot.LOT}");

        var totalOkQty = lot.LOT_QTY;
        var totalNgQty = 0m;
        var totalDefectQty = 0m;
        var notCheckoutQty = lot.LOT_QTY - lot.CUR_OPER_OUT_QTY;
        if (lot.CUR_OPER_OUT_QTY + totalOkQty + totalNgQty > lot.LOT_QTY)
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, $"Lot current allow out qty is {notCheckoutQty}: {lot.LOT}");

        var outQty = lot.LOT_QTY - lot.CUR_OPER_OUT_QTY - totalOkQty - totalNgQty;
        if (outQty != 0)
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, $"Lot still has report quantity pending, cannot check out: {lot.LOT}");

        var routeOperations = await GetRouteOperationsAsync(lot.ROUTE_SID, ct);
        var nextRouteOperation = routeOperations
            .Where(x => x.SEQ > lot.OPERATION_SEQ)
            .OrderBy(x => x.SEQ)
            .FirstOrDefault();
        var isFinished = nextRouteOperation == null;
        var nextOperation = nextRouteOperation == null
            ? null
            : await GetOperationBySidAsync(nextRouteOperation.WIP_OPERATION_SID, ct);

        var previousStatus = new WipLotStatusDto { LOT_STATUS_SID = lot.LOT_STATUS_SID, LOT_STATUS_CODE = lot.LOT_STATUS_CODE };
        var targetStatus = await GetLotStatusAsync(isFinished ? LotCheckOutCodes.FinishedStatus : LotCheckOutCodes.WaitStatus, ct);
        var nextOperationLinkSid = isFinished ? (decimal?)null : RandomHelper.GenerateRandomDecimal();
        var controlMode = input.GROUP_IN_USER ? LotCheckOutCodes.ControlModeGroup : LotCheckOutCodes.ControlModeOne;
        var nextSeq = await GetNextLotHistorySequenceInTxAsync(conn, tx, lot.LOT_SID, ct);
        var preStatusChangeTime = lot.LAST_STATUS_CHANGE_TIME;

        var checkOutHist = BuildLotHistoryRecord(
            histSid: RandomHelper.GenerateRandomDecimal(),
            seq: nextSeq,
            dataLinkSid: input.DATA_LINK_SID,
            lot: lot,
            currentStatus: targetStatus,
            previousStatus: previousStatus,
            operationLinkSid: operationLinkSid,
            operationSid: lot.OPERATION_SID,
            operationCode: operation.WIP_OPERATION_NO,
            operationName: operation.WIP_OPERATION_NAME ?? operation.WIP_OPERATION_NO,
            operationSeq: lot.OPERATION_SEQ,
            operationFinish: "Y",
            totalOkQty: totalOkQty,
            totalNgQty: totalNgQty,
            totalDefectQty: totalDefectQty,
            totalUserCount: activeUserHistories.Count,
            routeSid: lot.ROUTE_SID,
            factorySid: lot.FACTORY_SID,
            actionCode: LotCheckOutCodes.CheckOut,
            controlMode: controlMode,
            inputFormName: input.INPUT_FORM_NAME,
            createUser: input.ACCOUNT_NO,
            createTime: createTime,
            reportTime: reportTime,
            preReportTime: reportTime,
            preStatusChangeTime: preStatusChangeTime,
            lotQty1: lot.LOT_QTY1,
            lotQty2: lot.LOT_QTY2,
            location: lot.LOCATION,
            operFirstCheckInTime: lot.CUR_OPER_FIRST_IN_TIME,
            shiftSid: shiftSid,
            workgroupSid: workgroupSid,
            lotSubStatusCode: lot.LOT_SUB_STATUS_CODE,
            comment: input.COMMENT ?? string.Empty);

        var operEndHist = BuildLotHistoryRecord(
            histSid: RandomHelper.GenerateRandomDecimal(),
            seq: nextSeq + 1,
            dataLinkSid: input.DATA_LINK_SID,
            lot: lot,
            currentStatus: targetStatus,
            previousStatus: previousStatus,
            operationLinkSid: operationLinkSid,
            operationSid: lot.OPERATION_SID,
            operationCode: operation.WIP_OPERATION_NO,
            operationName: operation.WIP_OPERATION_NAME ?? operation.WIP_OPERATION_NO,
            operationSeq: lot.OPERATION_SEQ,
            operationFinish: "Y",
            totalOkQty: 0,
            totalNgQty: 0,
            totalDefectQty: 0,
            totalUserCount: activeUserHistories.Count,
            routeSid: lot.ROUTE_SID,
            factorySid: lot.FACTORY_SID,
            actionCode: LotCheckOutCodes.OperEnd,
            controlMode: controlMode,
            inputFormName: input.INPUT_FORM_NAME,
            createUser: input.ACCOUNT_NO,
            createTime: createTime,
            reportTime: reportTime,
            preReportTime: reportTime,
            preStatusChangeTime: preStatusChangeTime,
            lotQty1: lot.LOT_QTY1,
            lotQty2: lot.LOT_QTY2,
            location: lot.LOCATION,
            operFirstCheckInTime: null,
            shiftSid: shiftSid,
            workgroupSid: workgroupSid,
            lotSubStatusCode: lot.LOT_SUB_STATUS_CODE,
            comment: input.COMMENT ?? string.Empty);

        if (nextRouteOperation != null && nextOperation != null)
        {
            operEndHist.NEXT_OPERATION_SID = nextOperation.WIP_OPERATION_SID;
            operEndHist.NEXT_OPERATION_CODE = nextOperation.WIP_OPERATION_NO;
            operEndHist.NEXT_OPERATION_NAME = nextOperation.WIP_OPERATION_NAME ?? nextOperation.WIP_OPERATION_NO;
            operEndHist.NEXT_OPERATION_SEQ = nextRouteOperation.SEQ;
        }

        WipLotHistDto? followupHist = null;
        if (nextRouteOperation != null && nextOperation != null && nextOperationLinkSid.HasValue)
        {
            followupHist = BuildLotHistoryRecord(
                histSid: RandomHelper.GenerateRandomDecimal(),
                seq: nextSeq + 2,
                dataLinkSid: input.DATA_LINK_SID,
                lot: lot,
                currentStatus: targetStatus,
                previousStatus: previousStatus,
                operationLinkSid: nextOperationLinkSid.Value,
                operationSid: nextOperation.WIP_OPERATION_SID,
                operationCode: nextOperation.WIP_OPERATION_NO,
                operationName: nextOperation.WIP_OPERATION_NAME ?? nextOperation.WIP_OPERATION_NO,
                operationSeq: nextRouteOperation.SEQ,
                operationFinish: Flags.OperationFinishNo,
                totalOkQty: 0,
                totalNgQty: 0,
                totalDefectQty: 0,
                totalUserCount: activeUserHistories.Count,
                routeSid: lot.ROUTE_SID,
                factorySid: lot.FACTORY_SID,
                actionCode: LotCheckOutCodes.OperStart,
                controlMode: controlMode,
                inputFormName: input.INPUT_FORM_NAME,
                createUser: input.ACCOUNT_NO,
                createTime: createTime,
                reportTime: reportTime,
                preReportTime: reportTime,
                preStatusChangeTime: preStatusChangeTime,
                lotQty1: lot.LOT_QTY1,
                lotQty2: lot.LOT_QTY2,
                location: lot.LOCATION,
                operFirstCheckInTime: null,
                shiftSid: shiftSid,
                workgroupSid: workgroupSid,
                lotSubStatusCode: lot.LOT_SUB_STATUS_CODE,
                comment: input.COMMENT ?? string.Empty);
        }
        else if (isFinished)
        {
            followupHist = BuildLotHistoryRecord(
                histSid: RandomHelper.GenerateRandomDecimal(),
                seq: nextSeq + 2,
                dataLinkSid: input.DATA_LINK_SID,
                lot: lot,
                currentStatus: targetStatus,
                previousStatus: previousStatus,
                operationLinkSid: operationLinkSid,
                operationSid: lot.OPERATION_SID,
                operationCode: operation.WIP_OPERATION_NO,
                operationName: operation.WIP_OPERATION_NAME ?? operation.WIP_OPERATION_NO,
                operationSeq: lot.OPERATION_SEQ,
                operationFinish: "Y",
                totalOkQty: 0,
                totalNgQty: 0,
                totalDefectQty: 0,
                totalUserCount: activeUserHistories.Count,
                routeSid: lot.ROUTE_SID,
                factorySid: lot.FACTORY_SID,
                actionCode: LotCheckOutCodes.EndLot,
                controlMode: controlMode,
                inputFormName: input.INPUT_FORM_NAME,
                createUser: input.ACCOUNT_NO,
                createTime: createTime,
                reportTime: reportTime,
                preReportTime: reportTime,
                preStatusChangeTime: preStatusChangeTime,
                lotQty1: lot.LOT_QTY1,
                lotQty2: lot.LOT_QTY2,
                location: lot.LOCATION,
                operFirstCheckInTime: null,
                shiftSid: shiftSid,
                workgroupSid: workgroupSid,
                lotSubStatusCode: lot.LOT_SUB_STATUS_CODE,
                comment: input.COMMENT ?? string.Empty);
        }

        var originalEnableAuditColumns = _sqlHelper.EnableAuditColumns;
        _sqlHelper.EnableAuditColumns = false;
        try
        {
            await _sqlHelper.InsertInTxAsync(conn, tx, checkOutHist, ct: ct);
            await _sqlHelper.InsertInTxAsync(conn, tx, operEndHist, ct: ct);
            if (followupHist != null)
                await _sqlHelper.InsertInTxAsync(conn, tx, followupHist, ct: ct);
        }
        finally
        {
            _sqlHelper.EnableAuditColumns = originalEnableAuditColumns;
        }

        await DeleteCurrentLotEquipmentInTxAsync(conn, tx, lot.LOT_SID, ct);
        await DeleteCurrentLotUsersInTxAsync(conn, tx, lot.LOT_SID, ct);
        await CloseLotUserHistoriesInTxAsync(
            conn,
            tx,
            activeUserHistories,
            checkOutHist.WIP_LOT_HIST_SID,
            createTime,
            reportTime,
            input.ACCOUNT_NO,
            shiftSid,
            workgroupSid,
            totalOkQty,
            totalNgQty,
            "Y",
            lot.LOT_SUB_STATUS_CODE,
            ct);
        await UpdateLotCheckOutStateInTxAsync(conn, tx, lot, targetStatus, input.ACCOUNT_NO, createTime, reportTime, nextRouteOperation, nextOperation, nextOperationLinkSid, ct);
    }

    /// <summary>
    /// 將尚未進站的 LOT 重新指定到同一路由的另一個站別。
    /// 這支功能只允許 Wait 狀態的 LOT 使用，並會補寫重派站別歷程、當前站別結束歷程與新站別開始歷程。
    /// </summary>
    private async Task LotReassignOperationInTxAsync(
        SqlConnection conn,
        SqlTransaction tx,
        WipLotReassignOperationInputDto input,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(input.LOT))
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, "LOT is required.");

        if (string.IsNullOrWhiteSpace(input.ACCOUNT_NO))
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, "ACCOUNT_NO is required.");

        if (input.DATA_LINK_SID <= 0)
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, "DATA_LINK_SID must be greater than 0.");

        if (input.NEW_OPER_SEQ <= 0)
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, "NEW_OPER_SEQ must be greater than 0.");

        var lot = await GetLotByCodeInTxAsync(conn, tx, input.LOT, ct);
        if (!string.Equals(lot.LOT_STATUS_CODE, LotReassignOperationCodes.WaitStatus, StringComparison.OrdinalIgnoreCase))
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, $"LOT status must be Wait: {input.LOT}");

        var routeOperations = await GetRouteOperationsAsync(lot.ROUTE_SID, ct);
        var newRouteOperation = routeOperations.FirstOrDefault(x => x.SEQ == input.NEW_OPER_SEQ);
        if (newRouteOperation == null)
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, $"The NEW OPER SEQ does not exist: {input.NEW_OPER_SEQ}");

        var user = await GetUserByAccountAsync(input.ACCOUNT_NO, ct);
        var currentOperation = await GetOperationBySidAsync(lot.OPERATION_SID, ct);
        var newOperation = await GetOperationBySidAsync(newRouteOperation.WIP_OPERATION_SID, ct);
        var waitStatus = await GetLotStatusAsync(LotReassignOperationCodes.WaitStatus, ct);
        var reportTime = input.REPORT_TIME ?? await GetDbNowInTxAsync(conn, tx, ct);
        var createTime = await GetDbNowInTxAsync(conn, tx, ct);
        var currentOperationLinkSid = ParseOperationLinkSid(lot.CUR_OPERATION_LINK_SID, lot.LOT);
        var newOperationLinkSid = RandomHelper.GenerateRandomDecimal();
        var nextSeq = await GetNextLotHistorySequenceInTxAsync(conn, tx, lot.LOT_SID, ct);
        var previousStatus = new WipLotStatusDto { LOT_STATUS_SID = lot.LOT_STATUS_SID, LOT_STATUS_CODE = lot.LOT_STATUS_CODE };
        var shiftSid = user.SHIFT_SID ?? 0;
        var workgroupSid = user.WORKGROUP_SID ?? 0;

        var reassignHist = BuildLotHistoryRecord(
            histSid: RandomHelper.GenerateRandomDecimal(),
            seq: nextSeq,
            dataLinkSid: input.DATA_LINK_SID,
            lot: lot,
            currentStatus: waitStatus,
            previousStatus: previousStatus,
            operationLinkSid: currentOperationLinkSid,
            operationSid: lot.OPERATION_SID,
            operationCode: currentOperation.WIP_OPERATION_NO,
            operationName: currentOperation.WIP_OPERATION_NAME ?? currentOperation.WIP_OPERATION_NO,
            operationSeq: lot.OPERATION_SEQ,
            operationFinish: Flags.OperationFinishNo,
            totalOkQty: 0,
            totalNgQty: 0,
            totalDefectQty: 0,
            totalUserCount: 1,
            routeSid: lot.ROUTE_SID,
            factorySid: lot.FACTORY_SID,
            actionCode: LotReassignOperationCodes.ReassignOperation,
            controlMode: string.Empty,
            inputFormName: input.INPUT_FORM_NAME,
            createUser: input.ACCOUNT_NO,
            createTime: createTime,
            reportTime: reportTime,
            preReportTime: reportTime,
            preStatusChangeTime: lot.LAST_STATUS_CHANGE_TIME,
            lotQty1: lot.LOT_QTY1,
            lotQty2: lot.LOT_QTY2,
            location: lot.LOCATION,
            operFirstCheckInTime: null,
            shiftSid: shiftSid,
            workgroupSid: workgroupSid,
            lotSubStatusCode: lot.LOT_SUB_STATUS_CODE,
            comment: input.COMMENT ?? string.Empty);
        reassignHist.NEXT_OPERATION_SID = newOperation.WIP_OPERATION_SID;
        reassignHist.NEXT_OPERATION_CODE = newOperation.WIP_OPERATION_NO;
        reassignHist.NEXT_OPERATION_NAME = newOperation.WIP_OPERATION_NAME ?? newOperation.WIP_OPERATION_NO;
        reassignHist.NEXT_OPERATION_SEQ = newRouteOperation.SEQ;

        var operEndHist = BuildLotHistoryRecord(
            histSid: RandomHelper.GenerateRandomDecimal(),
            seq: nextSeq + 1,
            dataLinkSid: input.DATA_LINK_SID,
            lot: lot,
            currentStatus: waitStatus,
            previousStatus: previousStatus,
            operationLinkSid: currentOperationLinkSid,
            operationSid: lot.OPERATION_SID,
            operationCode: currentOperation.WIP_OPERATION_NO,
            operationName: currentOperation.WIP_OPERATION_NAME ?? currentOperation.WIP_OPERATION_NO,
            operationSeq: lot.OPERATION_SEQ,
            operationFinish: "Y",
            totalOkQty: 0,
            totalNgQty: 0,
            totalDefectQty: 0,
            totalUserCount: 1,
            routeSid: lot.ROUTE_SID,
            factorySid: lot.FACTORY_SID,
            actionCode: LotReassignOperationCodes.OperEnd,
            controlMode: string.Empty,
            inputFormName: input.INPUT_FORM_NAME,
            createUser: input.ACCOUNT_NO,
            createTime: createTime,
            reportTime: reportTime,
            preReportTime: reportTime,
            preStatusChangeTime: lot.LAST_STATUS_CHANGE_TIME,
            lotQty1: lot.LOT_QTY1,
            lotQty2: lot.LOT_QTY2,
            location: lot.LOCATION,
            operFirstCheckInTime: null,
            shiftSid: shiftSid,
            workgroupSid: workgroupSid,
            lotSubStatusCode: lot.LOT_SUB_STATUS_CODE,
            comment: input.COMMENT ?? string.Empty);
        operEndHist.NEXT_OPERATION_SID = newOperation.WIP_OPERATION_SID;
        operEndHist.NEXT_OPERATION_CODE = newOperation.WIP_OPERATION_NO;
        operEndHist.NEXT_OPERATION_NAME = newOperation.WIP_OPERATION_NAME ?? newOperation.WIP_OPERATION_NO;
        operEndHist.NEXT_OPERATION_SEQ = newRouteOperation.SEQ;

        var operStartHist = BuildLotHistoryRecord(
            histSid: RandomHelper.GenerateRandomDecimal(),
            seq: nextSeq + 2,
            dataLinkSid: input.DATA_LINK_SID,
            lot: lot,
            currentStatus: waitStatus,
            previousStatus: previousStatus,
            operationLinkSid: newOperationLinkSid,
            operationSid: newOperation.WIP_OPERATION_SID,
            operationCode: newOperation.WIP_OPERATION_NO,
            operationName: newOperation.WIP_OPERATION_NAME ?? newOperation.WIP_OPERATION_NO,
            operationSeq: newRouteOperation.SEQ,
            operationFinish: Flags.OperationFinishNo,
            totalOkQty: 0,
            totalNgQty: 0,
            totalDefectQty: 0,
            totalUserCount: 1,
            routeSid: lot.ROUTE_SID,
            factorySid: lot.FACTORY_SID,
            actionCode: LotReassignOperationCodes.OperStart,
            controlMode: string.Empty,
            inputFormName: input.INPUT_FORM_NAME,
            createUser: input.ACCOUNT_NO,
            createTime: createTime,
            reportTime: reportTime,
            preReportTime: reportTime,
            preStatusChangeTime: lot.LAST_STATUS_CHANGE_TIME,
            lotQty1: lot.LOT_QTY1,
            lotQty2: lot.LOT_QTY2,
            location: lot.LOCATION,
            operFirstCheckInTime: null,
            shiftSid: shiftSid,
            workgroupSid: workgroupSid,
            lotSubStatusCode: lot.LOT_SUB_STATUS_CODE,
            comment: input.COMMENT ?? string.Empty);

        var originalEnableAuditColumns = _sqlHelper.EnableAuditColumns;
        _sqlHelper.EnableAuditColumns = false;
        try
        {
            await _sqlHelper.InsertInTxAsync(conn, tx, reassignHist, ct: ct);
            await _sqlHelper.InsertInTxAsync(conn, tx, operEndHist, ct: ct);
            await _sqlHelper.InsertInTxAsync(conn, tx, operStartHist, ct: ct);
        }
        finally
        {
            _sqlHelper.EnableAuditColumns = originalEnableAuditColumns;
        }

        await UpdateLotReassignOperationStateInTxAsync(
            conn,
            tx,
            lot,
            waitStatus,
            newRouteOperation,
            newOperation,
            newOperationLinkSid,
            input.ACCOUNT_NO,
            createTime,
            ct);
    }

    /// <summary>
    /// 記錄 LOT 的 DC 收集資料。
    /// 這支功能會將每一筆收集值寫入 WIP_LOT_DC_ITEM_HIST，並同步 upsert 到 WIP_LOT_DC_ITEM_CURRENT，
    /// 同時補一筆 WIP_LOT_HIST 作為此次收集動作的交易歷程。
    /// </summary>
    private async Task LotRecordDcInTxAsync(
        SqlConnection conn,
        SqlTransaction tx,
        WipLotRecordDcInputDto input,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(input.LOT))
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, "LOT is required.");

        if (string.IsNullOrWhiteSpace(input.ACCOUNT_NO))
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, "ACCOUNT_NO is required.");

        if (input.DATA_LINK_SID <= 0)
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, "DATA_LINK_SID must be greater than 0.");

        if (input.ITEMS == null || input.ITEMS.Count == 0)
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, "ITEMS are required.");

        var lot = await GetLotByCodeInTxAsync(conn, tx, input.LOT, ct);
        var user = await GetUserByAccountAsync(input.ACCOUNT_NO, ct);
        var operation = await GetOperationBySidAsync(lot.OPERATION_SID, ct);
        var currentStatus = await GetLotStatusBySidAsync(lot.LOT_STATUS_SID, ct);
        var previousStatus = new WipLotStatusDto { LOT_STATUS_SID = lot.LOT_STATUS_SID, LOT_STATUS_CODE = lot.LOT_STATUS_CODE };
        var reportTime = input.REPORT_TIME ?? await GetDbNowInTxAsync(conn, tx, ct);
        var createTime = await GetDbNowInTxAsync(conn, tx, ct);
        var operationLinkSid = ParseOperationLinkSid(lot.CUR_OPERATION_LINK_SID, lot.LOT);
        var shiftSid = input.SHIFT_SID ?? user.SHIFT_SID ?? 0;
        var workgroupSid = user.WORKGROUP_SID ?? 0;
        var nextSeq = await GetNextLotHistorySequenceInTxAsync(conn, tx, lot.LOT_SID, ct);
        var histSid = RandomHelper.GenerateRandomDecimal();

        await UpsertLotDcItemsInTxAsync(conn, tx, lot, histSid, input, createTime, ct);

        var recordHist = BuildLotHistoryRecord(
            histSid: histSid,
            seq: nextSeq,
            dataLinkSid: input.DATA_LINK_SID,
            lot: lot,
            currentStatus: currentStatus,
            previousStatus: previousStatus,
            operationLinkSid: operationLinkSid,
            operationSid: lot.OPERATION_SID,
            operationCode: operation.WIP_OPERATION_NO,
            operationName: operation.WIP_OPERATION_NAME ?? operation.WIP_OPERATION_NO,
            operationSeq: lot.OPERATION_SEQ,
            operationFinish: Flags.OperationFinishNo,
            totalOkQty: 0,
            totalNgQty: 0,
            totalDefectQty: 0,
            totalUserCount: 1,
            routeSid: lot.ROUTE_SID,
            factorySid: lot.FACTORY_SID,
            actionCode: string.IsNullOrWhiteSpace(input.ACTION_CODE) ? LotRecordDcCodes.DefaultActionCode : input.ACTION_CODE,
            controlMode: string.Empty,
            inputFormName: input.INPUT_FORM_NAME,
            createUser: input.ACCOUNT_NO,
            createTime: createTime,
            reportTime: reportTime,
            preReportTime: createTime,
            preStatusChangeTime: lot.LAST_STATUS_CHANGE_TIME,
            lotQty1: lot.LOT_QTY1,
            lotQty2: lot.LOT_QTY2,
            location: lot.LOCATION,
            operFirstCheckInTime: null,
            shiftSid: shiftSid,
            workgroupSid: workgroupSid,
            lotSubStatusCode: lot.LOT_SUB_STATUS_CODE,
            comment: input.COMMENT ?? string.Empty);

        var originalEnableAuditColumns = _sqlHelper.EnableAuditColumns;
        _sqlHelper.EnableAuditColumns = false;
        try
        {
            await _sqlHelper.InsertInTxAsync(conn, tx, recordHist, ct: ct);
        }
        finally
        {
            _sqlHelper.EnableAuditColumns = originalEnableAuditColumns;
        }

        await TouchLotInTxAsync(conn, tx, lot, input.ACCOUNT_NO, createTime, ct);
    }

    /// <summary>
    /// 將指定 LOT 設為 Hold。
    /// 核心行為比照舊案：
    /// 1. 驗證 LOT 目前狀態只能是 Wait / Run / Hold。
    /// 2. 寫入一筆 WIP_LOT_HOLD_HIST，保存本次 Hold 原因與 Hold 前狀態。
    /// 3. 寫入一筆 WIP_LOT_HIST，記錄 LOT_HOLD 歷程。
    /// 4. 更新 WIP_LOT 狀態為 Hold。
    /// </summary>
    /// <summary>
    /// 將指定 LOT 設為 Hold。
    /// 單層 Hold 模型只允許 Wait 或 Run 進入 Hold，已經是 Hold 的 LOT 不允許再次 Hold。
    /// </summary>
    private async Task LotHoldInTxAsync(
        SqlConnection conn,
        SqlTransaction tx,
        WipLotHoldInputDto input,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(input.LOT))
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, "LOT is required.");

        if (string.IsNullOrWhiteSpace(input.ACCOUNT_NO))
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, "ACCOUNT_NO is required.");

        if (input.REASON_SID <= 0)
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, "REASON_SID must be greater than 0.");

        if (input.DATA_LINK_SID <= 0)
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, "DATA_LINK_SID must be greater than 0.");

        var lot = await GetLotByCodeInTxAsync(conn, tx, input.LOT, ct);
        if (string.Equals(lot.LOT_STATUS_CODE, LotHoldCodes.HoldStatus, StringComparison.OrdinalIgnoreCase))
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, $"LOT is already on hold and cannot be held again: {input.LOT}");

        if (!LotHoldCodes.AllowHoldStatuses.Any(status => string.Equals(status, lot.LOT_STATUS_CODE, StringComparison.OrdinalIgnoreCase)))
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, $"LOT status must be Wait or Run: {input.LOT}");

        var user = await GetUserByAccountAsync(input.ACCOUNT_NO, ct);
        var operation = await GetOperationBySidAsync(lot.OPERATION_SID, ct);
        var holdStatus = await GetLotStatusAsync(LotHoldCodes.HoldStatus, ct);
        var previousStatus = new WipLotStatusDto { LOT_STATUS_SID = lot.LOT_STATUS_SID, LOT_STATUS_CODE = lot.LOT_STATUS_CODE };
        var reason = await GetReasonBySidAsync(input.REASON_SID, ct);
        var reportTime = input.REPORT_TIME ?? await GetDbNowInTxAsync(conn, tx, ct);
        var createTime = await GetDbNowInTxAsync(conn, tx, ct);
        var operationLinkSid = ParseOperationLinkSid(lot.CUR_OPERATION_LINK_SID, lot.LOT);
        var shiftSid = user.SHIFT_SID ?? 0;
        var workgroupSid = user.WORKGROUP_SID ?? 0;
        var nextSeq = await GetNextLotHistorySequenceInTxAsync(conn, tx, lot.LOT_SID, ct);

        var lotHoldHist = new WipLotHoldHistDto
        {
            WIP_LOT_HOLD_HIST_SID = RandomHelper.GenerateRandomDecimal(),
            HOLD_WIP_LOT_HIST_SID = RandomHelper.GenerateRandomDecimal(),
            HOLD_REASON_SID = reason.ADM_REASON_SID,
            HOLD_REASON_CODE = reason.REASON_NO,
            HOLD_REASON_NAME = reason.REASON_NAME,
            HOLD_REASON_COMMENT = input.COMMENT,
            PRE_LOT_STATUS_SID = previousStatus.LOT_STATUS_SID,
            LOT = lot.LOT,
            RELEASE_FLAG = Flags.No
        };

        var holdHist = BuildLotHistoryRecord(
            histSid: lotHoldHist.HOLD_WIP_LOT_HIST_SID,
            seq: nextSeq,
            dataLinkSid: input.DATA_LINK_SID,
            lot: lot,
            currentStatus: holdStatus,
            previousStatus: previousStatus,
            operationLinkSid: operationLinkSid,
            operationSid: lot.OPERATION_SID,
            operationCode: operation.WIP_OPERATION_NO,
            operationName: operation.WIP_OPERATION_NAME ?? operation.WIP_OPERATION_NO,
            operationSeq: lot.OPERATION_SEQ,
            operationFinish: Flags.OperationFinishNo,
            totalOkQty: 0,
            totalNgQty: 0,
            totalDefectQty: 0,
            totalUserCount: 1,
            routeSid: lot.ROUTE_SID,
            factorySid: lot.FACTORY_SID,
            actionCode: LotHoldCodes.Hold,
            controlMode: string.Empty,
            inputFormName: input.INPUT_FORM_NAME,
            createUser: input.ACCOUNT_NO,
            createTime: createTime,
            reportTime: reportTime,
            preReportTime: createTime,
            preStatusChangeTime: lot.LAST_STATUS_CHANGE_TIME,
            lotQty1: lot.LOT_QTY1,
            lotQty2: lot.LOT_QTY2,
            location: lot.LOCATION,
            operFirstCheckInTime: null,
            shiftSid: shiftSid,
            workgroupSid: workgroupSid,
            lotSubStatusCode: lot.LOT_SUB_STATUS_CODE,
            comment: input.COMMENT ?? string.Empty);

        var originalEnableAuditColumns = _sqlHelper.EnableAuditColumns;
        _sqlHelper.EnableAuditColumns = false;
        try
        {
            await _sqlHelper.InsertInTxAsync(conn, tx, lotHoldHist, ct: ct);
            await _sqlHelper.InsertInTxAsync(conn, tx, holdHist, ct: ct);
        }
        finally
        {
            _sqlHelper.EnableAuditColumns = originalEnableAuditColumns;
        }

        await UpdateLotStatusInTxAsync(conn, tx, lot, holdStatus, input.ACCOUNT_NO, createTime, reportTime, ct);
    }

    /// <summary>
    /// 解除指定 LOT 的某一筆 Hold 紀錄。
    /// 核心行為比照舊案：
    /// 1. LOT 目前必須是 Hold。
    /// 2. 指定的 WIP_LOT_HOLD_HIST 必須存在且尚未 Release。
    /// 3. 更新該筆 WIP_LOT_HOLD_HIST 的 release 原因與 release flag。
    /// 4. 寫入一筆 WIP_LOT_HIST，記錄 LOT_HOLD_RELEASE 歷程。
    /// 5. 若這是最後一筆未解除 Hold，則將 WIP_LOT 狀態還原到 Hold 前狀態；否則維持 Hold。
    /// </summary>
    /// <summary>
    /// 解除指定 LOT 的 Hold。
    /// 目前採單層 Hold 模型，若查到多筆未解除 Hold 紀錄，視為異常資料並直接中止。
    /// </summary>
    private async Task LotHoldReleaseInTxAsync(
        SqlConnection conn,
        SqlTransaction tx,
        WipLotHoldReleaseInputDto input,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(input.LOT))
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, "LOT is required.");

        if (string.IsNullOrWhiteSpace(input.ACCOUNT_NO))
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, "ACCOUNT_NO is required.");

        if (input.LOT_HOLD_SID <= 0)
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, "LOT_HOLD_SID must be greater than 0.");

        if (input.REASON_SID <= 0)
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, "REASON_SID must be greater than 0.");

        if (input.DATA_LINK_SID <= 0)
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, "DATA_LINK_SID must be greater than 0.");

        var lot = await GetLotByCodeInTxAsync(conn, tx, input.LOT, ct);
        if (!string.Equals(lot.LOT_STATUS_CODE, LotHoldCodes.HoldStatus, StringComparison.OrdinalIgnoreCase))
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, $"LOT status must be Hold: {input.LOT}");

        var user = await GetUserByAccountAsync(input.ACCOUNT_NO, ct);
        var operation = await GetOperationBySidAsync(lot.OPERATION_SID, ct);
        var reason = await GetReasonBySidAsync(input.REASON_SID, ct);
        var reportTime = input.REPORT_TIME ?? await GetDbNowInTxAsync(conn, tx, ct);
        var createTime = await GetDbNowInTxAsync(conn, tx, ct);
        var operationLinkSid = ParseOperationLinkSid(lot.CUR_OPERATION_LINK_SID, lot.LOT);
        var shiftSid = user.SHIFT_SID ?? 0;
        var workgroupSid = user.WORKGROUP_SID ?? 0;
        var openHoldHistories = await GetOpenLotHoldHistoriesInTxAsync(conn, tx, lot.LOT, ct);
        if (openHoldHistories.Count > 1)
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.Conflict, $"Multiple unreleased hold records were found for LOT: {input.LOT}");

        var targetHoldHistory = openHoldHistories.FirstOrDefault(x => x.WIP_LOT_HOLD_HIST_SID == input.LOT_HOLD_SID);
        if (targetHoldHistory == null)
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, $"Open lot hold history not found: {input.LOT_HOLD_SID}");

        var restoredStatus = await GetLotStatusBySidAsync(targetHoldHistory.PRE_LOT_STATUS_SID, ct);
        var previousStatus = new WipLotStatusDto { LOT_STATUS_SID = lot.LOT_STATUS_SID, LOT_STATUS_CODE = lot.LOT_STATUS_CODE };
        var nextSeq = await GetNextLotHistorySequenceInTxAsync(conn, tx, lot.LOT_SID, ct);

        var releaseHist = BuildLotHistoryRecord(
            histSid: RandomHelper.GenerateRandomDecimal(),
            seq: nextSeq,
            dataLinkSid: input.DATA_LINK_SID,
            lot: lot,
            currentStatus: restoredStatus,
            previousStatus: previousStatus,
            operationLinkSid: operationLinkSid,
            operationSid: lot.OPERATION_SID,
            operationCode: operation.WIP_OPERATION_NO,
            operationName: operation.WIP_OPERATION_NAME ?? operation.WIP_OPERATION_NO,
            operationSeq: lot.OPERATION_SEQ,
            operationFinish: Flags.OperationFinishNo,
            totalOkQty: 0,
            totalNgQty: 0,
            totalDefectQty: 0,
            totalUserCount: 1,
            routeSid: lot.ROUTE_SID,
            factorySid: lot.FACTORY_SID,
            actionCode: LotHoldCodes.HoldRelease,
            controlMode: string.Empty,
            inputFormName: input.INPUT_FORM_NAME,
            createUser: input.ACCOUNT_NO,
            createTime: createTime,
            reportTime: reportTime,
            preReportTime: lot.LAST_STATUS_CHANGE_TIME,
            preStatusChangeTime: lot.LAST_STATUS_CHANGE_TIME,
            lotQty1: lot.LOT_QTY1,
            lotQty2: lot.LOT_QTY2,
            location: lot.LOCATION,
            operFirstCheckInTime: null,
            shiftSid: shiftSid,
            workgroupSid: workgroupSid,
            lotSubStatusCode: lot.LOT_SUB_STATUS_CODE,
            comment: input.COMMENT ?? string.Empty);

        var originalEnableAuditColumns = _sqlHelper.EnableAuditColumns;
        _sqlHelper.EnableAuditColumns = false;
        try
        {
            await _sqlHelper.InsertInTxAsync(conn, tx, releaseHist, ct: ct);
        }
        finally
        {
            _sqlHelper.EnableAuditColumns = originalEnableAuditColumns;
        }

        await UpdateLotHoldHistoryReleaseInTxAsync(conn, tx, targetHoldHistory, releaseHist.WIP_LOT_HIST_SID, reason, input.COMMENT, ct);
        await UpdateLotStatusInTxAsync(conn, tx, lot, restoredStatus, input.ACCOUNT_NO, createTime, reportTime, ct);
    }

    /// <summary>
    /// 追加 LOT 數量。
    /// 舊案以 LOT_BONUS 表示數量追加；新版保留同一個交易語意：
    /// 寫入 WIP_LOT_HIST、WIP_LOT_REASON_HIST，並在同一個交易中增加 WIP_LOT.LOT_QTY。
    /// </summary>
    private async Task LotBonusInTxAsync(
        SqlConnection conn,
        SqlTransaction tx,
        WipLotBonusInputDto input,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        ValidateLotBonusInput(input);

        var lot = await GetLotByCodeInTxAsync(conn, tx, input.LOT, ct);
        var user = await GetUserByAccountAsync(input.ACCOUNT_NO, ct);
        var operation = await GetOperationBySidAsync(lot.OPERATION_SID, ct);
        var currentStatus = new WipLotStatusDto { LOT_STATUS_SID = lot.LOT_STATUS_SID, LOT_STATUS_CODE = lot.LOT_STATUS_CODE };
        var reason = await GetReasonBySidAsync(input.REASON_SID, ct);
        var reportTime = input.REPORT_TIME ?? await GetDbNowInTxAsync(conn, tx, ct);
        var createTime = await GetDbNowInTxAsync(conn, tx, ct);
        var operationLinkSid = ParseOperationLinkSid(lot.CUR_OPERATION_LINK_SID, lot.LOT);
        var histSid = RandomHelper.GenerateRandomDecimal();

        var hist = BuildLotHistoryRecord(
            histSid: histSid,
            seq: await GetNextLotHistorySequenceInTxAsync(conn, tx, lot.LOT_SID, ct),
            dataLinkSid: input.DATA_LINK_SID,
            lot: lot,
            currentStatus: currentStatus,
            previousStatus: currentStatus,
            operationLinkSid: operationLinkSid,
            operationSid: lot.OPERATION_SID,
            operationCode: operation.WIP_OPERATION_NO,
            operationName: operation.WIP_OPERATION_NAME ?? operation.WIP_OPERATION_NO,
            operationSeq: lot.OPERATION_SEQ,
            operationFinish: Flags.OperationFinishNo,
            totalOkQty: input.BONUS_QTY,
            totalNgQty: 0,
            totalDefectQty: 0,
            totalUserCount: 1,
            routeSid: lot.ROUTE_SID,
            factorySid: lot.FACTORY_SID,
            actionCode: LotQuantityAdjustCodes.BonusAction,
            controlMode: string.Empty,
            inputFormName: input.INPUT_FORM_NAME,
            createUser: input.ACCOUNT_NO,
            createTime: createTime,
            reportTime: reportTime,
            preReportTime: lot.LAST_TRANS_TIME ?? lot.LAST_STATUS_CHANGE_TIME,
            preStatusChangeTime: lot.LAST_STATUS_CHANGE_TIME,
            lotQty1: lot.LOT_QTY1,
            lotQty2: lot.LOT_QTY2,
            location: lot.LOCATION,
            operFirstCheckInTime: null,
            shiftSid: user.SHIFT_SID ?? 0,
            workgroupSid: user.WORKGROUP_SID ?? 0,
            lotSubStatusCode: lot.LOT_SUB_STATUS_CODE,
            comment: input.COMMENT ?? string.Empty);

        var reasonHist = BuildLotReasonHistoryRecord(
            histSid,
            LotQuantityAdjustCodes.BonusReasonType,
            reason,
            input.BONUS_QTY,
            input.COMMENT);

        var originalEnableAuditColumns = _sqlHelper.EnableAuditColumns;
        _sqlHelper.EnableAuditColumns = false;
        try
        {
            await _sqlHelper.InsertInTxAsync(conn, tx, hist, ct: ct);
            await _sqlHelper.InsertInTxAsync(conn, tx, reasonHist, ct: ct);
        }
        finally
        {
            _sqlHelper.EnableAuditColumns = originalEnableAuditColumns;
        }

        await UpdateLotBonusQuantityInTxAsync(conn, tx, lot, input.BONUS_QTY, input.ACCOUNT_NO, createTime, ct);
    }

    /// <summary>
    /// 報廢 LOT 數量。
    /// 舊案以 LOT_NG 表示 Scrap/NG；新版不改變 LOT 狀態，只在同一個交易中扣減 LOT_QTY、
    /// 累加 NG_QTY，並寫入 WIP_LOT_HIST 與 WIP_LOT_REASON_HIST 保留原因與數量。
    /// </summary>
    private async Task LotScrapInTxAsync(
        SqlConnection conn,
        SqlTransaction tx,
        WipLotScrapInputDto input,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        ValidateLotScrapInput(input);

        var lot = await GetLotByCodeInTxAsync(conn, tx, input.LOT, ct);
        if (lot.LOT_QTY - input.SCRAP_QTY < 0)
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, $"SCRAP_QTY cannot be greater than current LOT_QTY. LOT={input.LOT}, LOT_QTY={lot.LOT_QTY}, SCRAP_QTY={input.SCRAP_QTY}");

        var user = await GetUserByAccountAsync(input.ACCOUNT_NO, ct);
        var operation = await GetOperationBySidAsync(lot.OPERATION_SID, ct);
        var currentStatus = new WipLotStatusDto { LOT_STATUS_SID = lot.LOT_STATUS_SID, LOT_STATUS_CODE = lot.LOT_STATUS_CODE };
        var reason = await GetReasonBySidAsync(input.REASON_SID, ct);
        var reportTime = input.REPORT_TIME ?? await GetDbNowInTxAsync(conn, tx, ct);
        var createTime = await GetDbNowInTxAsync(conn, tx, ct);
        var operationLinkSid = ParseOperationLinkSid(lot.CUR_OPERATION_LINK_SID, lot.LOT);
        var histSid = RandomHelper.GenerateRandomDecimal();

        var hist = BuildLotHistoryRecord(
            histSid: histSid,
            seq: await GetNextLotHistorySequenceInTxAsync(conn, tx, lot.LOT_SID, ct),
            dataLinkSid: input.DATA_LINK_SID,
            lot: lot,
            currentStatus: currentStatus,
            previousStatus: currentStatus,
            operationLinkSid: operationLinkSid,
            operationSid: lot.OPERATION_SID,
            operationCode: operation.WIP_OPERATION_NO,
            operationName: operation.WIP_OPERATION_NAME ?? operation.WIP_OPERATION_NO,
            operationSeq: lot.OPERATION_SEQ,
            operationFinish: Flags.OperationFinishNo,
            totalOkQty: 0,
            totalNgQty: input.SCRAP_QTY,
            totalDefectQty: 0,
            totalUserCount: 1,
            routeSid: lot.ROUTE_SID,
            factorySid: lot.FACTORY_SID,
            actionCode: LotQuantityAdjustCodes.ScrapAction,
            controlMode: string.Empty,
            inputFormName: input.INPUT_FORM_NAME,
            createUser: input.ACCOUNT_NO,
            createTime: createTime,
            reportTime: reportTime,
            preReportTime: lot.LAST_TRANS_TIME ?? lot.LAST_STATUS_CHANGE_TIME,
            preStatusChangeTime: lot.LAST_STATUS_CHANGE_TIME,
            lotQty1: lot.LOT_QTY1,
            lotQty2: lot.LOT_QTY2,
            location: lot.LOCATION,
            operFirstCheckInTime: null,
            shiftSid: user.SHIFT_SID ?? 0,
            workgroupSid: user.WORKGROUP_SID ?? 0,
            lotSubStatusCode: lot.LOT_SUB_STATUS_CODE,
            comment: input.COMMENT ?? string.Empty);

        var reasonHist = BuildLotReasonHistoryRecord(
            histSid,
            LotQuantityAdjustCodes.ScrapReasonType,
            reason,
            input.SCRAP_QTY,
            input.COMMENT);

        var originalEnableAuditColumns = _sqlHelper.EnableAuditColumns;
        _sqlHelper.EnableAuditColumns = false;
        try
        {
            await _sqlHelper.InsertInTxAsync(conn, tx, hist, ct: ct);
            await _sqlHelper.InsertInTxAsync(conn, tx, reasonHist, ct: ct);
        }
        finally
        {
            _sqlHelper.EnableAuditColumns = originalEnableAuditColumns;
        }

        await UpdateLotScrapQuantityInTxAsync(conn, tx, lot, input.SCRAP_QTY, input.ACCOUNT_NO, createTime, ct);
    }

    /// <summary>
    /// 變更 LOT 狀態的共用核心。
    /// 舊案 LotStateChange、LotTerminated、LotUnTerminated、LotFinished、LotUnFinished 都是同一組語意：
    /// 寫入 WIP_LOT_HIST、WIP_LOT_REASON_HIST，並在同一個交易中更新 WIP_LOT 的狀態與交易時間。
    /// </summary>
    private async Task LotStateChangeInTxAsync(
        SqlConnection conn,
        SqlTransaction tx,
        WipLotStateChangeInputDto input,
        string actionCode,
        string targetStatusCode,
        string[]? allowedCurrentStatusCodes,
        CancellationToken ct)
    {
        ValidateLotStateChangeInput(input, targetStatusCode);

        var lot = await GetLotByCodeInTxAsync(conn, tx, input.LOT, ct);
        if (allowedCurrentStatusCodes is { Length: > 0 }
            && !allowedCurrentStatusCodes.Any(status => string.Equals(lot.LOT_STATUS_CODE, status, StringComparison.OrdinalIgnoreCase)))
        {
            var allowedStatusMessage = string.Join(" or ", allowedCurrentStatusCodes);
            throw new HttpStatusCodeException(
                System.Net.HttpStatusCode.BadRequest,
                $"LOT status must be {allowedStatusMessage}: {input.LOT}");
        }

        var user = await GetUserByAccountAsync(input.ACCOUNT_NO, ct);
        var operation = await GetOperationBySidAsync(lot.OPERATION_SID, ct);
        var currentStatus = await GetLotStatusBySidAsync(lot.LOT_STATUS_SID, ct);
        var targetStatus = await GetLotStatusAsync(targetStatusCode, ct);
        var reason = await GetReasonBySidAsync(input.REASON_SID, ct);
        var reportTime = input.REPORT_TIME ?? await GetDbNowInTxAsync(conn, tx, ct);
        var createTime = await GetDbNowInTxAsync(conn, tx, ct);
        var operationLinkSid = ParseOperationLinkSid(lot.CUR_OPERATION_LINK_SID, lot.LOT);

        var histSid = RandomHelper.GenerateRandomDecimal();
        var hist = BuildLotHistoryRecord(
            histSid,
            seq: await GetNextLotHistorySequenceInTxAsync(conn, tx, lot.LOT_SID, ct),
            dataLinkSid: input.DATA_LINK_SID,
            lot: lot,
            currentStatus: targetStatus,
            previousStatus: currentStatus,
            operationLinkSid: operationLinkSid,
            operationSid: operation.WIP_OPERATION_SID,
            operationCode: operation.WIP_OPERATION_NO,
            operationName: operation.WIP_OPERATION_NAME ?? operation.WIP_OPERATION_NO,
            operationSeq: lot.OPERATION_SEQ,
            operationFinish: Flags.OperationFinishNo,
            totalOkQty: 0,
            totalNgQty: 0,
            totalDefectQty: 0,
            totalUserCount: 0,
            routeSid: lot.ROUTE_SID,
            factorySid: lot.FACTORY_SID,
            actionCode: actionCode,
            controlMode: string.Empty,
            inputFormName: input.INPUT_FORM_NAME,
            createUser: input.ACCOUNT_NO,
            createTime: createTime,
            reportTime: reportTime,
            preReportTime: lot.LAST_TRANS_TIME ?? lot.LAST_STATUS_CHANGE_TIME,
            preStatusChangeTime: lot.LAST_STATUS_CHANGE_TIME,
            lotQty1: lot.LOT_QTY1,
            lotQty2: lot.LOT_QTY2,
            location: lot.LOCATION,
            operFirstCheckInTime: lot.CUR_OPER_FIRST_IN_TIME,
            shiftSid: user.SHIFT_SID ?? 0,
            workgroupSid: user.WORKGROUP_SID ?? 0,
            lotSubStatusCode: lot.LOT_SUB_STATUS_CODE,
            comment: input.COMMENT ?? string.Empty);

        var reasonHist = BuildLotReasonHistoryRecord(
            histSid,
            LotStateChangeCodes.NormalReasonType,
            reason,
            reasonQty: 0,
            input.COMMENT);

        var originalEnableAuditColumns = _sqlHelper.EnableAuditColumns;
        _sqlHelper.EnableAuditColumns = false;
        try
        {
            await _sqlHelper.InsertInTxAsync(conn, tx, hist, ct: ct);
            await _sqlHelper.InsertInTxAsync(conn, tx, reasonHist, ct: ct);
        }
        finally
        {
            _sqlHelper.EnableAuditColumns = originalEnableAuditColumns;
        }

        await UpdateLotStatusInTxAsync(conn, tx, lot, targetStatus, input.ACCOUNT_NO, createTime, reportTime, ct);
    }

    private static void ValidateLotBonusInput(WipLotBonusInputDto input)
    {
        if (string.IsNullOrWhiteSpace(input.LOT))
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, "LOT is required.");

        if (string.IsNullOrWhiteSpace(input.ACCOUNT_NO))
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, "ACCOUNT_NO is required.");

        if (input.BONUS_QTY <= 0)
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, "BONUS_QTY must be greater than 0.");

        if (input.REASON_SID <= 0)
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, "REASON_SID must be greater than 0.");

        if (input.DATA_LINK_SID <= 0)
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, "DATA_LINK_SID must be greater than 0.");
    }

    private static void ValidateLotScrapInput(WipLotScrapInputDto input)
    {
        if (string.IsNullOrWhiteSpace(input.LOT))
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, "LOT is required.");

        if (string.IsNullOrWhiteSpace(input.ACCOUNT_NO))
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, "ACCOUNT_NO is required.");

        if (input.SCRAP_QTY <= 0)
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, "SCRAP_QTY must be greater than 0.");

        if (input.REASON_SID <= 0)
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, "REASON_SID must be greater than 0.");

        if (input.DATA_LINK_SID <= 0)
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, "DATA_LINK_SID must be greater than 0.");
    }

    private static void ValidateLotStateChangeInput(WipLotStateChangeInputDto input, string targetStatusCode)
    {
        if (string.IsNullOrWhiteSpace(input.LOT))
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, "LOT is required.");

        if (string.IsNullOrWhiteSpace(targetStatusCode))
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, "NEW_STATE_CODE is required.");

        if (string.IsNullOrWhiteSpace(input.ACCOUNT_NO))
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, "ACCOUNT_NO is required.");

        if (input.REASON_SID <= 0)
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, "REASON_SID must be greater than 0.");

        if (input.DATA_LINK_SID <= 0)
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, "DATA_LINK_SID must be greater than 0.");
    }

    private static WipLotReasonHistDto BuildLotReasonHistoryRecord(
        decimal wipLotHistSid,
        string reasonType,
        AdmReasonDto reason,
        decimal reasonQty,
        string? comment)
    {
        return new WipLotReasonHistDto
        {
            WIP_LOT_REASON_HIST_SID = RandomHelper.GenerateRandomDecimal(),
            WIP_LOT_HIST_SID = wipLotHistSid,
            REASON_TYPE = reasonType,
            REASON_SID = reason.ADM_REASON_SID,
            REASON_CODE = reason.REASON_NO,
            REASON_NAME = reason.REASON_NAME,
            REASON_COMMENT = comment,
            REASON_QTY = reasonQty,
            REASON_QTY1 = null,
            REASON_QTY2 = null
        };
    }

    private WipLotHistDto BuildCreateLotHistory(
        decimal histSid,
        int seq,
        WipCreateLotInputDto input,
        WipLotDto lot,
        WipLotStatusDto currentStatus,
        WipLotStatusDto previousStatus,
        decimal operationLinkSid,
        WipOperationDto operation,
        WipRouteOperationDto routeOperation,
        WipWoDto workOrder,
        AdmUserDto user,
        DateTime now)
    {
        return new WipLotHistDto
        {
            WIP_LOT_HIST_SID = histSid,
            SEQ = seq,
            DATA_LINK_SID = input.DATA_LINK_SID,
            LOT_SID = lot.LOT_SID,
            LOT = lot.LOT,
            ALIAS_LOT1 = lot.ALIAS_LOT1,
            ALIAS_LOT2 = lot.ALIAS_LOT2,
            LOT_STATUS_SID = currentStatus.LOT_STATUS_SID,
            LOT_STATUS_CODE = currentStatus.LOT_STATUS_CODE,
            PRE_LOT_STATUS_SID = previousStatus.LOT_STATUS_SID,
            PRE_LOT_STATUS_CODE = previousStatus.LOT_STATUS_CODE,
            WO_SID = workOrder.WO_SID,
            WO = workOrder.WO,
            OPERATION_LINK_SID = operationLinkSid,
            OPERATION_SID = operation.WIP_OPERATION_SID,
            OPERATION_CODE = operation.WIP_OPERATION_NO,
            OPERATION_NAME = operation.WIP_OPERATION_NAME ?? routeOperation.NAME ?? operation.WIP_OPERATION_NO,
            OPERATION_SEQ = routeOperation.SEQ,
            OPERATION_FINISH = Flags.OperationFinishNo,
            PART_SID = lot.PART_SID,
            PART_NO = lot.PART_NO,
            LOT_QTY = input.LOT_QTY,
            TOTAL_OK_QTY = 0,
            TOTAL_NG_QTY = 0,
            TOTAL_DEFECT_QTY = 0,
            ROUTE_SID = input.ROUTE_SID,
            FACTORY_SID = (workOrder.FACTORY_SID ?? 0).ToString(),
            FACTORY_CODE = null,
            FACTORY_NAME = null,
            ACTION_CODE = CreateLotActionCodes.CreateLot,
            CONTROL_MODE = string.Empty,
            INPUT_FORM_NAME = input.INPUT_FORM_NAME,
            CREATE_USER = input.ACCOUNT_NO,
            CREATE_TIME = now,
            REPORT_TIME = input.REPORT_TIME,
            PRE_REPORT_TIME = input.REPORT_TIME,
            PRE_STATUS_CHANGE_TIME = input.REPORT_TIME,
            SHIFT_SID = user.SHIFT_SID ?? 0,
            WORKGROUP_SID = user.WORKGROUP_SID ?? 0,
            COMMENT = input.COMMENT ?? string.Empty
        };
    }

    private static WipLotHistDto BuildLotHistoryRecord(
        decimal histSid,
        int seq,
        decimal dataLinkSid,
        WipLotDto lot,
        WipLotStatusDto currentStatus,
        WipLotStatusDto previousStatus,
        decimal operationLinkSid,
        decimal operationSid,
        string operationCode,
        string operationName,
        decimal operationSeq,
        string operationFinish,
        decimal totalOkQty,
        decimal totalNgQty,
        decimal totalDefectQty,
        int totalUserCount,
        decimal routeSid,
        decimal factorySid,
        string actionCode,
        string controlMode,
        string? inputFormName,
        string createUser,
        DateTime createTime,
        DateTime reportTime,
        DateTime preReportTime,
        DateTime preStatusChangeTime,
        decimal? lotQty1,
        decimal? lotQty2,
        string? location,
        DateTime? operFirstCheckInTime,
        decimal shiftSid,
        decimal workgroupSid,
        string? lotSubStatusCode,
        string? comment)
    {
        return new WipLotHistDto
        {
            WIP_LOT_HIST_SID = histSid,
            SEQ = seq,
            DATA_LINK_SID = dataLinkSid,
            LOT_SID = lot.LOT_SID,
            LOT = lot.LOT,
            ALIAS_LOT1 = lot.ALIAS_LOT1,
            ALIAS_LOT2 = lot.ALIAS_LOT2,
            LOT_STATUS_SID = currentStatus.LOT_STATUS_SID,
            LOT_STATUS_CODE = currentStatus.LOT_STATUS_CODE,
            PRE_LOT_STATUS_SID = previousStatus.LOT_STATUS_SID,
            PRE_LOT_STATUS_CODE = previousStatus.LOT_STATUS_CODE,
            WO_SID = lot.WO_SID,
            WO = lot.WO,
            OPERATION_LINK_SID = operationLinkSid,
            OPERATION_SID = operationSid,
            OPERATION_CODE = operationCode,
            OPERATION_NAME = operationName,
            OPERATION_SEQ = operationSeq,
            OPERATION_FINISH = operationFinish,
            PART_SID = lot.PART_SID,
            PART_NO = lot.PART_NO,
            LOT_QTY = lot.LOT_QTY,
            TOTAL_OK_QTY = totalOkQty,
            TOTAL_NG_QTY = totalNgQty,
            TOTAL_DEFECT_QTY = totalDefectQty,
            TOTAL_USER_COUNT = totalUserCount,
            ROUTE_SID = routeSid,
            FACTORY_SID = factorySid.ToString(),
            ACTION_CODE = actionCode,
            CONTROL_MODE = controlMode,
            INPUT_FORM_NAME = inputFormName,
            CREATE_USER = createUser,
            CREATE_TIME = createTime,
            REPORT_TIME = reportTime,
            PRE_REPORT_TIME = preReportTime,
            PRE_STATUS_CHANGE_TIME = preStatusChangeTime,
            LOT_QTY1 = lotQty1,
            LOT_QTY2 = lotQty2,
            LOCATION = location,
            OPER_FIRST_CHECK_IN_TIME = operFirstCheckInTime,
            SHIFT_SID = shiftSid,
            WORKGROUP_SID = workgroupSid,
            LOT_SUB_STATUS_CODE = lotSubStatusCode,
            COMMENT = comment
        };
    }

    private async Task<List<WipRouteOperationDto>> GetRouteOperationsAsync(decimal routeSid, CancellationToken ct)
    {
        var where = new WhereBuilder<WipRouteOperationDto>()
            .AndEq(x => x.WIP_ROUTE_SID, routeSid);

        return (await _sqlHelper.SelectWhereAsync(where, ct))
            .OrderBy(x => x.SEQ)
            .ToList();
    }

    private async Task<List<WipLotHoldHistDto>> GetOpenLotHoldHistoriesInTxAsync(
        SqlConnection conn,
        SqlTransaction tx,
        string lot,
        CancellationToken ct)
    {
        var where = new WhereBuilder<WipLotHoldHistDto>()
            .AndEq(x => x.LOT, lot)
            .AndEq(x => x.RELEASE_FLAG, Flags.No);

        return (await _sqlHelper.SelectWhereInTxAsync(conn, tx, where, ct: ct)).ToList();
    }

    private async Task<List<WipLotUserHistDto>> GetActiveLotUserHistoriesInTxAsync(
        SqlConnection conn,
        SqlTransaction tx,
        decimal lotSid,
        decimal operationLinkSid,
        CancellationToken ct)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
                          SELECT h.*
                          FROM [WIP_LOT_USER_HIST] h
                          INNER JOIN [WIP_LOT_HIST] lh
                              ON lh.[WIP_LOT_HIST_SID] = h.[IN_WIP_LOT_HIST_SID]
                          WHERE lh.[LOT_SID] = @LotSid
                            AND h.[OPERATION_LINK_SID] = @OperationLinkSid
                            AND h.[OUT_FLAG] = 'N'
                          ORDER BY h.[CREATE_IN_TIME] DESC;
                          """;
        cmd.Parameters.Add(new SqlParameter("@LotSid", SqlDbType.Decimal) { Value = lotSid });
        cmd.Parameters.Add(new SqlParameter("@OperationLinkSid", SqlDbType.Decimal) { Value = operationLinkSid });

        var histories = new List<WipLotUserHistDto>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            histories.Add(new WipLotUserHistDto
            {
                WIP_LOT_USER_HIST_SID = reader.GetDecimal(reader.GetOrdinal("WIP_LOT_USER_HIST_SID")),
                IN_WIP_LOT_HIST_SID = reader.GetDecimal(reader.GetOrdinal("IN_WIP_LOT_HIST_SID")),
                OUT_WIP_LOT_HIST_SID = reader.IsDBNull(reader.GetOrdinal("OUT_WIP_LOT_HIST_SID")) ? null : reader.GetDecimal(reader.GetOrdinal("OUT_WIP_LOT_HIST_SID")),
                CREATE_USER = reader.GetString(reader.GetOrdinal("CREATE_USER")),
                USER_COMMENT = reader.IsDBNull(reader.GetOrdinal("USER_COMMENT")) ? null : reader.GetString(reader.GetOrdinal("USER_COMMENT")),
                CREATE_IN_TIME = reader.GetDateTime(reader.GetOrdinal("CREATE_IN_TIME")),
                REPORT_IN_TIME = reader.GetDateTime(reader.GetOrdinal("REPORT_IN_TIME")),
                CREATE_OUT_TIME = reader.IsDBNull(reader.GetOrdinal("CREATE_OUT_TIME")) ? null : reader.GetDateTime(reader.GetOrdinal("CREATE_OUT_TIME")),
                REPORT_OUT_TIME = reader.IsDBNull(reader.GetOrdinal("REPORT_OUT_TIME")) ? null : reader.GetDateTime(reader.GetOrdinal("REPORT_OUT_TIME")),
                OUT_FLAG = reader.GetString(reader.GetOrdinal("OUT_FLAG")),
                OUT_OK_QTY = reader.GetDecimal(reader.GetOrdinal("OUT_OK_QTY")),
                OUT_NG_QTY = reader.GetDecimal(reader.GetOrdinal("OUT_NG_QTY")),
                REPORT_OUT_OK_QTY = reader.GetDecimal(reader.GetOrdinal("REPORT_OUT_OK_QTY")),
                REPORT_OUT_NG_QTY = reader.GetDecimal(reader.GetOrdinal("REPORT_OUT_NG_QTY")),
                OPERATION_FINISH = reader.IsDBNull(reader.GetOrdinal("OPERATION_FINISH")) ? null : reader.GetString(reader.GetOrdinal("OPERATION_FINISH")),
                OPERATION_LINK_SID = reader.GetDecimal(reader.GetOrdinal("OPERATION_LINK_SID")),
                SHIFT_SID = reader.IsDBNull(reader.GetOrdinal("SHIFT_SID")) ? null : reader.GetDecimal(reader.GetOrdinal("SHIFT_SID")),
                WORKGROUP_SID = reader.IsDBNull(reader.GetOrdinal("WORKGROUP_SID")) ? null : reader.GetDecimal(reader.GetOrdinal("WORKGROUP_SID")),
                OUT_USER = reader.IsDBNull(reader.GetOrdinal("OUT_USER")) ? null : reader.GetString(reader.GetOrdinal("OUT_USER")),
                OUT_SHIFT_SID = reader.IsDBNull(reader.GetOrdinal("OUT_SHIFT_SID")) ? null : reader.GetDecimal(reader.GetOrdinal("OUT_SHIFT_SID")),
                OUT_WORKGROUP_SID = reader.IsDBNull(reader.GetOrdinal("OUT_WORKGROUP_SID")) ? null : reader.GetDecimal(reader.GetOrdinal("OUT_WORKGROUP_SID")),
                LOT_SUB_STATUS_CODE = reader.IsDBNull(reader.GetOrdinal("LOT_SUB_STATUS_CODE")) ? null : reader.GetString(reader.GetOrdinal("LOT_SUB_STATUS_CODE"))
            });
        }

        return histories;
    }

    private static async Task DeleteCurrentLotUsersInTxAsync(SqlConnection conn, SqlTransaction tx, decimal lotSid, CancellationToken ct)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "DELETE FROM [WIP_LOT_CUR_USER] WHERE [LOT_SID] = @LotSid;";
        cmd.Parameters.Add(new SqlParameter("@LotSid", SqlDbType.Decimal) { Value = lotSid });
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task DeleteCurrentLotEquipmentInTxAsync(SqlConnection conn, SqlTransaction tx, decimal lotSid, CancellationToken ct)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "DELETE FROM [WIP_LOT_CUR_EQP] WHERE [LOT_SID] = @LotSid;";
        cmd.Parameters.Add(new SqlParameter("@LotSid", SqlDbType.Decimal) { Value = lotSid });
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task CloseLotUserHistoriesInTxAsync(
        SqlConnection conn,
        SqlTransaction tx,
        IReadOnlyCollection<WipLotUserHistDto> userHistories,
        decimal outWipLotHistSid,
        DateTime createOutTime,
        DateTime reportOutTime,
        string outUser,
        decimal outShiftSid,
        decimal outWorkgroupSid,
        decimal outOkQty,
        decimal outNgQty,
        string operationFinish,
        string? lotSubStatusCode,
        CancellationToken ct)
    {
        foreach (var userHistory in userHistories)
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = """
                              UPDATE [WIP_LOT_USER_HIST]
                              SET [OUT_FLAG] = 'Y',
                                  [OUT_OK_QTY] = @OutOkQty,
                                  [OUT_NG_QTY] = @OutNgQty,
                                  [REPORT_OUT_OK_QTY] = @ReportOutOkQty,
                                  [REPORT_OUT_NG_QTY] = @ReportOutNgQty,
                                  [OUT_WIP_LOT_HIST_SID] = @OutWipLotHistSid,
                                  [OPERATION_FINISH] = @OperationFinish,
                                  [CREATE_OUT_TIME] = @CreateOutTime,
                                  [REPORT_OUT_TIME] = @ReportOutTime,
                                  [OUT_SHIFT_SID] = @OutShiftSid,
                                  [OUT_WORKGROUP_SID] = @OutWorkgroupSid,
                                  [OUT_USER] = @OutUser,
                                  [LOT_SUB_STATUS_CODE] = @LotSubStatusCode
                              WHERE [WIP_LOT_USER_HIST_SID] = @WipLotUserHistSid;
                              """;
            cmd.Parameters.Add(new SqlParameter("@OutOkQty", SqlDbType.Decimal) { Value = outOkQty });
            cmd.Parameters.Add(new SqlParameter("@OutNgQty", SqlDbType.Decimal) { Value = outNgQty });
            cmd.Parameters.Add(new SqlParameter("@ReportOutOkQty", SqlDbType.Decimal) { Value = outOkQty });
            cmd.Parameters.Add(new SqlParameter("@ReportOutNgQty", SqlDbType.Decimal) { Value = outNgQty });
            cmd.Parameters.Add(new SqlParameter("@OutWipLotHistSid", SqlDbType.Decimal) { Value = outWipLotHistSid });
            cmd.Parameters.Add(new SqlParameter("@OperationFinish", SqlDbType.NVarChar, 10) { Value = operationFinish });
            cmd.Parameters.Add(new SqlParameter("@CreateOutTime", SqlDbType.DateTime) { Value = createOutTime });
            cmd.Parameters.Add(new SqlParameter("@ReportOutTime", SqlDbType.DateTime) { Value = reportOutTime });
            cmd.Parameters.Add(new SqlParameter("@OutShiftSid", SqlDbType.Decimal) { Value = outShiftSid });
            cmd.Parameters.Add(new SqlParameter("@OutWorkgroupSid", SqlDbType.Decimal) { Value = outWorkgroupSid });
            cmd.Parameters.Add(new SqlParameter("@OutUser", SqlDbType.NVarChar, 50) { Value = outUser });
            cmd.Parameters.Add(new SqlParameter("@LotSubStatusCode", SqlDbType.NVarChar, 50) { Value = (object?)lotSubStatusCode ?? DBNull.Value });
            cmd.Parameters.Add(new SqlParameter("@WipLotUserHistSid", SqlDbType.Decimal) { Value = userHistory.WIP_LOT_USER_HIST_SID });
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }

    private static async Task UpdateLotCheckInCancelStateInTxAsync(
        SqlConnection conn,
        SqlTransaction tx,
        WipLotDto lot,
        WipLotStatusDto waitStatus,
        string editUser,
        DateTime editTime,
        DateTime reportTime,
        CancellationToken ct)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
                          UPDATE [WIP_LOT]
                          SET [LOT_STATUS_SID] = @LotStatusSid,
                              [LOT_STATUS_CODE] = @LotStatusCode,
                              [LOT_SUB_STATUS_CODE] = '',
                              [LAST_STATUS_CHANGE_TIME] = @ReportTime,
                              [LAST_TRANS_TIME] = @EditTime,
                              [EDIT_USER] = @EditUser,
                              [EDIT_TIME] = @EditTime
                          WHERE [LOT_SID] = @LotSid
                            AND [EDIT_TIME] = @OriginalEditTime;
                          """;
        cmd.Parameters.Add(new SqlParameter("@LotStatusSid", SqlDbType.Decimal) { Value = waitStatus.LOT_STATUS_SID });
        cmd.Parameters.Add(new SqlParameter("@LotStatusCode", SqlDbType.NVarChar, 50) { Value = waitStatus.LOT_STATUS_CODE });
        cmd.Parameters.Add(new SqlParameter("@ReportTime", SqlDbType.DateTime) { Value = reportTime });
        cmd.Parameters.Add(new SqlParameter("@EditUser", SqlDbType.NVarChar, 50) { Value = editUser });
        cmd.Parameters.Add(new SqlParameter("@EditTime", SqlDbType.DateTime) { Value = editTime });
        cmd.Parameters.Add(new SqlParameter("@LotSid", SqlDbType.Decimal) { Value = lot.LOT_SID });
        cmd.Parameters.Add(new SqlParameter("@OriginalEditTime", SqlDbType.DateTime) { Value = lot.EDIT_TIME ?? lot.CREATE_TIME ?? reportTime });

        var affectedRows = await cmd.ExecuteNonQueryAsync(ct);
        if (affectedRows != 1)
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.Conflict, $"LOT check-in cancel failed because the data was modified concurrently: {lot.LOT}");
    }

    private static async Task UpdateLotReassignOperationStateInTxAsync(
        SqlConnection conn,
        SqlTransaction tx,
        WipLotDto lot,
        WipLotStatusDto waitStatus,
        WipRouteOperationDto nextRouteOperation,
        WipOperationDto nextOperation,
        decimal nextOperationLinkSid,
        string editUser,
        DateTime editTime,
        CancellationToken ct)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
                          UPDATE [WIP_LOT]
                          SET [LOT_STATUS_SID] = @LotStatusSid,
                              [LOT_STATUS_CODE] = @LotStatusCode,
                              [ROUTE_OPER_SID] = @RouteOperSid,
                              [ALL_OPER_BATCH_ID] = @LotSid,
                              [CUR_OPER_BATCH_ID] = @LotSid,
                              [CUR_OPERATION_LINK_SID] = CONVERT(nvarchar(50), @NextOperationLinkSid),
                              [OPERATION_SID] = @NextOperationSid,
                              [OPERATION_SEQ] = @NextOperationSeq,
                              [LAST_TRANS_TIME] = @EditTime,
                              [EDIT_USER] = @EditUser,
                              [EDIT_TIME] = @EditTime
                          WHERE [LOT_SID] = @LotSid
                            AND [EDIT_TIME] = @OriginalEditTime;
                          """;
        cmd.Parameters.Add(new SqlParameter("@LotStatusSid", SqlDbType.Decimal) { Value = waitStatus.LOT_STATUS_SID });
        cmd.Parameters.Add(new SqlParameter("@LotStatusCode", SqlDbType.NVarChar, 50) { Value = waitStatus.LOT_STATUS_CODE });
        cmd.Parameters.Add(new SqlParameter("@RouteOperSid", SqlDbType.Decimal) { Value = nextRouteOperation.WIP_ROUTE_OPERATION_SID });
        cmd.Parameters.Add(new SqlParameter("@LotSid", SqlDbType.Decimal) { Value = lot.LOT_SID });
        cmd.Parameters.Add(new SqlParameter("@NextOperationLinkSid", SqlDbType.Decimal) { Value = nextOperationLinkSid });
        cmd.Parameters.Add(new SqlParameter("@NextOperationSid", SqlDbType.Decimal) { Value = nextOperation.WIP_OPERATION_SID });
        cmd.Parameters.Add(new SqlParameter("@NextOperationSeq", SqlDbType.Decimal) { Value = nextRouteOperation.SEQ });
        cmd.Parameters.Add(new SqlParameter("@EditUser", SqlDbType.NVarChar, 50) { Value = editUser });
        cmd.Parameters.Add(new SqlParameter("@EditTime", SqlDbType.DateTime) { Value = editTime });
        cmd.Parameters.Add(new SqlParameter("@OriginalEditTime", SqlDbType.DateTime) { Value = lot.EDIT_TIME ?? lot.CREATE_TIME ?? editTime });

        var affectedRows = await cmd.ExecuteNonQueryAsync(ct);
        if (affectedRows != 1)
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.Conflict, $"LOT reassign operation failed because the data was modified concurrently: {lot.LOT}");
    }

    private static async Task UpdateLotStatusInTxAsync(
        SqlConnection conn,
        SqlTransaction tx,
        WipLotDto lot,
        WipLotStatusDto targetStatus,
        string editUser,
        DateTime editTime,
        DateTime reportTime,
        CancellationToken ct)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
                          UPDATE [WIP_LOT]
                          SET [LOT_STATUS_SID] = @LotStatusSid,
                              [LOT_STATUS_CODE] = @LotStatusCode,
                              [LAST_STATUS_CHANGE_TIME] = @ReportTime,
                              [LAST_TRANS_TIME] = @EditTime,
                              [EDIT_USER] = @EditUser,
                              [EDIT_TIME] = @EditTime
                          WHERE [LOT_SID] = @LotSid
                            AND [EDIT_TIME] = @OriginalEditTime;
                          """;
        cmd.Parameters.Add(new SqlParameter("@LotStatusSid", SqlDbType.Decimal) { Value = targetStatus.LOT_STATUS_SID });
        cmd.Parameters.Add(new SqlParameter("@LotStatusCode", SqlDbType.NVarChar, 50) { Value = targetStatus.LOT_STATUS_CODE });
        cmd.Parameters.Add(new SqlParameter("@ReportTime", SqlDbType.DateTime) { Value = reportTime });
        cmd.Parameters.Add(new SqlParameter("@EditUser", SqlDbType.NVarChar, 50) { Value = editUser });
        cmd.Parameters.Add(new SqlParameter("@EditTime", SqlDbType.DateTime) { Value = editTime });
        cmd.Parameters.Add(new SqlParameter("@LotSid", SqlDbType.Decimal) { Value = lot.LOT_SID });
        cmd.Parameters.Add(new SqlParameter("@OriginalEditTime", SqlDbType.DateTime) { Value = lot.EDIT_TIME ?? lot.CREATE_TIME ?? reportTime });

        var affectedRows = await cmd.ExecuteNonQueryAsync(ct);
        if (affectedRows != 1)
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.Conflict, $"LOT status update failed because the data was modified concurrently: {lot.LOT}");
    }

    private static async Task TouchLotInTxAsync(
        SqlConnection conn,
        SqlTransaction tx,
        WipLotDto lot,
        string editUser,
        DateTime editTime,
        CancellationToken ct)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
                          UPDATE [WIP_LOT]
                          SET [EDIT_USER] = @EditUser,
                              [LAST_TRANS_TIME] = @EditTime,
                              [EDIT_TIME] = @EditTime
                          WHERE [LOT_SID] = @LotSid
                            AND [EDIT_TIME] = @OriginalEditTime;
                          """;
        cmd.Parameters.Add(new SqlParameter("@EditUser", SqlDbType.NVarChar, 50) { Value = editUser });
        cmd.Parameters.Add(new SqlParameter("@EditTime", SqlDbType.DateTime) { Value = editTime });
        cmd.Parameters.Add(new SqlParameter("@LotSid", SqlDbType.Decimal) { Value = lot.LOT_SID });
        cmd.Parameters.Add(new SqlParameter("@OriginalEditTime", SqlDbType.DateTime) { Value = lot.EDIT_TIME ?? lot.CREATE_TIME ?? editTime });

        var affectedRows = await cmd.ExecuteNonQueryAsync(ct);
        if (affectedRows != 1)
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.Conflict, $"LOT touch update failed because the data was modified concurrently: {lot.LOT}");
    }

    private static async Task UpdateLotBonusQuantityInTxAsync(
        SqlConnection conn,
        SqlTransaction tx,
        WipLotDto lot,
        decimal bonusQty,
        string editUser,
        DateTime editTime,
        CancellationToken ct)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
                          UPDATE [WIP_LOT]
                          SET [LOT_QTY] = [LOT_QTY] + @BonusQty,
                              [LAST_TRANS_TIME] = @EditTime,
                              [EDIT_USER] = @EditUser,
                              [EDIT_TIME] = @EditTime
                          WHERE [LOT_SID] = @LotSid
                            AND [EDIT_TIME] = @OriginalEditTime;
                          """;
        cmd.Parameters.Add(new SqlParameter("@BonusQty", SqlDbType.Decimal) { Value = bonusQty });
        cmd.Parameters.Add(new SqlParameter("@EditUser", SqlDbType.NVarChar, 50) { Value = editUser });
        cmd.Parameters.Add(new SqlParameter("@EditTime", SqlDbType.DateTime) { Value = editTime });
        cmd.Parameters.Add(new SqlParameter("@LotSid", SqlDbType.Decimal) { Value = lot.LOT_SID });
        cmd.Parameters.Add(new SqlParameter("@OriginalEditTime", SqlDbType.DateTime) { Value = lot.EDIT_TIME ?? lot.CREATE_TIME ?? editTime });

        var affectedRows = await cmd.ExecuteNonQueryAsync(ct);
        if (affectedRows != 1)
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.Conflict, $"LOT bonus update failed because the data was modified concurrently: {lot.LOT}");
    }

    private static async Task UpdateLotScrapQuantityInTxAsync(
        SqlConnection conn,
        SqlTransaction tx,
        WipLotDto lot,
        decimal scrapQty,
        string editUser,
        DateTime editTime,
        CancellationToken ct)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
                          UPDATE [WIP_LOT]
                          SET [LOT_QTY] = [LOT_QTY] - @ScrapQty,
                              [NG_QTY] = [NG_QTY] + @ScrapQty,
                              [LAST_TRANS_TIME] = @EditTime,
                              [EDIT_USER] = @EditUser,
                              [EDIT_TIME] = @EditTime
                          WHERE [LOT_SID] = @LotSid
                            AND [EDIT_TIME] = @OriginalEditTime
                            AND [LOT_QTY] >= @ScrapQty;
                          """;
        cmd.Parameters.Add(new SqlParameter("@ScrapQty", SqlDbType.Decimal) { Value = scrapQty });
        cmd.Parameters.Add(new SqlParameter("@EditUser", SqlDbType.NVarChar, 50) { Value = editUser });
        cmd.Parameters.Add(new SqlParameter("@EditTime", SqlDbType.DateTime) { Value = editTime });
        cmd.Parameters.Add(new SqlParameter("@LotSid", SqlDbType.Decimal) { Value = lot.LOT_SID });
        cmd.Parameters.Add(new SqlParameter("@OriginalEditTime", SqlDbType.DateTime) { Value = lot.EDIT_TIME ?? lot.CREATE_TIME ?? editTime });

        var affectedRows = await cmd.ExecuteNonQueryAsync(ct);
        if (affectedRows != 1)
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.Conflict, $"LOT scrap update failed because the data was modified concurrently or quantity is insufficient: {lot.LOT}");
    }

    private async Task UpsertLotDcItemsInTxAsync(
        SqlConnection conn,
        SqlTransaction tx,
        WipLotDto lot,
        decimal wipLotHistSid,
        WipLotRecordDcInputDto input,
        DateTime createTime,
        CancellationToken ct)
    {
        var originalEnableAuditColumns = _sqlHelper.EnableAuditColumns;
        _sqlHelper.EnableAuditColumns = false;
        try
        {
            foreach (var item in input.ITEMS)
            {
                var dcItem = await GetDcItemAsync(item, ct);
                var hist = BuildLotDcHistRecord(wipLotHistSid, input, item, dcItem, createTime);
                var current = BuildLotDcCurrentRecord(lot.LOT, hist, input, item, dcItem, createTime);
                var existingCurrent = await GetLotDcItemCurrentInTxAsync(conn, tx, lot.LOT, current.DC_ITEM_CODE, ct);

                await _sqlHelper.InsertInTxAsync(conn, tx, hist, ct: ct);

                if (existingCurrent == null)
                {
                    await _sqlHelper.InsertInTxAsync(conn, tx, current, ct: ct);
                }
                else
                {
                    await UpdateLotDcItemCurrentInTxAsync(conn, tx, existingCurrent.WIP_LOT_DC_ITEM_CURRENT_SID, current, ct);
                }
            }
        }
        finally
        {
            _sqlHelper.EnableAuditColumns = originalEnableAuditColumns;
        }
    }

    private static WipLotDcItemHistDto BuildLotDcHistRecord(
        decimal wipLotHistSid,
        WipLotRecordDcInputDto input,
        WipLotRecordDcItemInputDto item,
        QmmDcItemDto dcItem,
        DateTime createTime)
    {
        return new WipLotDcItemHistDto
        {
            WIP_LOT_DC_HIST_SID = RandomHelper.GenerateRandomDecimal(),
            WIP_LOT_HIST_SID = wipLotHistSid,
            DATA_TYPE = item.DATA_TYPE ?? dcItem.DATA_TYPE,
            DC_TYPE = item.DC_TYPE ?? input.DC_TYPE,
            DC_ITEM_SID = dcItem.QMM_ITEM_SID,
            DC_ITEM_CODE = item.DC_ITEM_CODE ?? dcItem.QMM_ITEM_NO,
            DC_ITEM_NAME = item.DC_ITEM_NAME ?? dcItem.QMM_ITEM_NAME,
            DC_ITEM_SEQ = item.DC_ITEM_SEQ ?? 1,
            DC_ITEM_VALUE = item.DC_ITEM_VALUE,
            DC_ITEM_COMMENT = item.DC_ITEM_COMMENT,
            USL = item.USL ?? dcItem.USL,
            UCL = item.UCL ?? dcItem.UCL,
            TARGET = item.TARGET ?? dcItem.TARGET,
            LCL = item.LCL ?? dcItem.LCL,
            LSL = item.LSL ?? dcItem.LSL,
            THROW_SPC = item.THROW_SPC,
            THROW_SPC_RESULT = item.THROW_SPC_RESULT,
            SPC_RESULT_LINK_SID = item.SPC_RESULT_LINK_SID,
            RESULT = item.RESULT,
            RESULT_COMMENT = item.RESULT_COMMENT,
            QC_NO = item.QC_NO,
            CREATE_TIME = createTime
        };
    }

    private static WipLotDcItemCurrentDto BuildLotDcCurrentRecord(
        string lot,
        WipLotDcItemHistDto hist,
        WipLotRecordDcInputDto input,
        WipLotRecordDcItemInputDto item,
        QmmDcItemDto dcItem,
        DateTime createTime)
    {
        return new WipLotDcItemCurrentDto
        {
            WIP_LOT_DC_ITEM_CURRENT_SID = RandomHelper.GenerateRandomDecimal(),
            LOT = lot,
            WIP_LOT_DC_HIST_SID = hist.WIP_LOT_DC_HIST_SID,
            WIP_LOT_HIST_SID = hist.WIP_LOT_HIST_SID,
            DATA_TYPE = hist.DATA_TYPE,
            DC_TYPE = hist.DC_TYPE,
            DC_ITEM_SID = hist.DC_ITEM_SID,
            DC_ITEM_CODE = hist.DC_ITEM_CODE,
            DC_ITEM_NAME = hist.DC_ITEM_NAME,
            DC_ITEM_SEQ = hist.DC_ITEM_SEQ,
            DC_ITEM_VALUE = hist.DC_ITEM_VALUE,
            DC_ITEM_COMMENT = hist.DC_ITEM_COMMENT,
            USL = hist.USL,
            UCL = hist.UCL,
            TARGET = hist.TARGET,
            LCL = hist.LCL,
            LSL = hist.LSL,
            THROW_SPC = hist.THROW_SPC,
            THROW_SPC_RESULT = hist.THROW_SPC_RESULT,
            SPC_RESULT_LINK_SID = hist.SPC_RESULT_LINK_SID,
            RESULT = hist.RESULT,
            RESULT_COMMENT = hist.RESULT_COMMENT,
            QC_NO = hist.QC_NO,
            CREATE_TIME = createTime
        };
    }

    private async Task<WipLotDcItemCurrentDto?> GetLotDcItemCurrentInTxAsync(
        SqlConnection conn,
        SqlTransaction tx,
        string lot,
        string dcItemCode,
        CancellationToken ct)
    {
        var where = new WhereBuilder<WipLotDcItemCurrentDto>()
            .AndEq(x => x.LOT, lot)
            .AndEq(x => x.DC_ITEM_CODE, dcItemCode);

        return await _sqlHelper.SelectFirstOrDefaultInTxAsync(conn, tx, where, ct: ct);
    }

    private static async Task UpdateLotDcItemCurrentInTxAsync(
        SqlConnection conn,
        SqlTransaction tx,
        decimal currentSid,
        WipLotDcItemCurrentDto current,
        CancellationToken ct)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
                          UPDATE [WIP_LOT_DC_ITEM_CURRENT]
                          SET [WIP_LOT_DC_HIST_SID] = @WipLotDcHistSid,
                              [WIP_LOT_HIST_SID] = @WipLotHistSid,
                              [DATA_TYPE] = @DataType,
                              [DC_TYPE] = @DcType,
                              [DC_ITEM_SID] = @DcItemSid,
                              [DC_ITEM_NAME] = @DcItemName,
                              [DC_ITEM_SEQ] = @DcItemSeq,
                              [DC_ITEM_VALUE] = @DcItemValue,
                              [DC_ITEM_COMMENT] = @DcItemComment,
                              [USL] = @Usl,
                              [UCL] = @Ucl,
                              [TARGET] = @Target,
                              [LCL] = @Lcl,
                              [LSL] = @Lsl,
                              [THROW_SPC] = @ThrowSpc,
                              [THROW_SPC_RESULT] = @ThrowSpcResult,
                              [SPC_RESULT_LINK_SID] = @SpcResultLinkSid,
                              [RESULT] = @Result,
                              [RESULT_COMMENT] = @ResultComment,
                              [QC_NO] = @QcNo
                          WHERE [WIP_LOT_DC_ITEM_CURRENT_SID] = @CurrentSid;
                          """;
        cmd.Parameters.Add(new SqlParameter("@WipLotDcHistSid", SqlDbType.Decimal) { Value = current.WIP_LOT_DC_HIST_SID });
        cmd.Parameters.Add(new SqlParameter("@WipLotHistSid", SqlDbType.Decimal) { Value = current.WIP_LOT_HIST_SID });
        cmd.Parameters.Add(new SqlParameter("@DataType", SqlDbType.NVarChar, 50) { Value = (object?)current.DATA_TYPE ?? DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@DcType", SqlDbType.NVarChar, 50) { Value = (object?)current.DC_TYPE ?? DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@DcItemSid", SqlDbType.Decimal) { Value = current.DC_ITEM_SID });
        cmd.Parameters.Add(new SqlParameter("@DcItemName", SqlDbType.NVarChar, 200) { Value = current.DC_ITEM_NAME });
        cmd.Parameters.Add(new SqlParameter("@DcItemSeq", SqlDbType.Decimal) { Value = current.DC_ITEM_SEQ });
        cmd.Parameters.Add(new SqlParameter("@DcItemValue", SqlDbType.NVarChar, -1) { Value = (object?)current.DC_ITEM_VALUE ?? DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@DcItemComment", SqlDbType.NVarChar, -1) { Value = (object?)current.DC_ITEM_COMMENT ?? DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@Usl", SqlDbType.NVarChar, 100) { Value = (object?)current.USL ?? DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@Ucl", SqlDbType.NVarChar, 100) { Value = (object?)current.UCL ?? DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@Target", SqlDbType.NVarChar, 100) { Value = (object?)current.TARGET ?? DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@Lcl", SqlDbType.NVarChar, 100) { Value = (object?)current.LCL ?? DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@Lsl", SqlDbType.NVarChar, 100) { Value = (object?)current.LSL ?? DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@ThrowSpc", SqlDbType.Char, 1) { Value = (object?)current.THROW_SPC ?? DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@ThrowSpcResult", SqlDbType.Char, 1) { Value = (object?)current.THROW_SPC_RESULT ?? DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@SpcResultLinkSid", SqlDbType.Decimal) { Value = (object?)current.SPC_RESULT_LINK_SID ?? DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@Result", SqlDbType.NVarChar, 100) { Value = (object?)current.RESULT ?? DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@ResultComment", SqlDbType.NVarChar, -1) { Value = (object?)current.RESULT_COMMENT ?? DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@QcNo", SqlDbType.NVarChar, 100) { Value = (object?)current.QC_NO ?? DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@CurrentSid", SqlDbType.Decimal) { Value = currentSid });

        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task UpdateLotHoldHistoryReleaseInTxAsync(
        SqlConnection conn,
        SqlTransaction tx,
        WipLotHoldHistDto holdHistory,
        decimal releaseWipLotHistSid,
        AdmReasonDto reason,
        string? comment,
        CancellationToken ct)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
                          UPDATE [WIP_LOT_HOLD_HIST]
                          SET [RELEASE_WIP_LOT_HIST_SID] = @ReleaseWipLotHistSid,
                              [RELEASE_REASON_SID] = @ReleaseReasonSid,
                              [RELEASE_REASON_CODE] = @ReleaseReasonCode,
                              [RELEASE_REASON_NAME] = @ReleaseReasonName,
                              [RELEASE_REASON_COMMENT] = @ReleaseReasonComment,
                              [RELEASE_FLAG] = 'Y'
                          WHERE [WIP_LOT_HOLD_HIST_SID] = @LotHoldSid
                            AND [RELEASE_FLAG] = 'N';
                          """;
        cmd.Parameters.Add(new SqlParameter("@ReleaseWipLotHistSid", SqlDbType.Decimal) { Value = releaseWipLotHistSid });
        cmd.Parameters.Add(new SqlParameter("@ReleaseReasonSid", SqlDbType.Decimal) { Value = reason.ADM_REASON_SID });
        cmd.Parameters.Add(new SqlParameter("@ReleaseReasonCode", SqlDbType.NVarChar, 50) { Value = reason.REASON_NO });
        cmd.Parameters.Add(new SqlParameter("@ReleaseReasonName", SqlDbType.NVarChar, 100) { Value = reason.REASON_NAME });
        cmd.Parameters.Add(new SqlParameter("@ReleaseReasonComment", SqlDbType.NVarChar) { Value = (object?)comment ?? DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@LotHoldSid", SqlDbType.Decimal) { Value = holdHistory.WIP_LOT_HOLD_HIST_SID });

        var affectedRows = await cmd.ExecuteNonQueryAsync(ct);
        if (affectedRows != 1)
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.Conflict, $"LOT hold history release failed because the data was modified concurrently: {holdHistory.WIP_LOT_HOLD_HIST_SID}");
    }

    private static async Task UpdateLotCheckOutStateInTxAsync(
        SqlConnection conn,
        SqlTransaction tx,
        WipLotDto lot,
        WipLotStatusDto targetStatus,
        string editUser,
        DateTime editTime,
        DateTime reportTime,
        WipRouteOperationDto? nextRouteOperation,
        WipOperationDto? nextOperation,
        decimal? nextOperationLinkSid,
        CancellationToken ct)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
                          UPDATE [WIP_LOT]
                          SET [LOT_STATUS_SID] = @LotStatusSid,
                              [LOT_STATUS_CODE] = @LotStatusCode,
                              [LOT_SUB_STATUS_CODE] = @LotSubStatusCode,
                              [LAST_STATUS_CHANGE_TIME] = @ReportTime,
                              [LAST_TRANS_TIME] = @EditTime,
                              [EDIT_USER] = @EditUser,
                              [EDIT_TIME] = @EditTime,
                              [CUR_OPER_OUT_QTY] = 0,
                              [CUR_OPER_NG_OUT_QTY] = 0,
                              [CUR_OPER_BATCH_ID] = @LotSid,
                              [ALL_OPER_BATCH_ID] = CASE WHEN @IsFinished = 1 THEN @LotSid ELSE [ALL_OPER_BATCH_ID] END,
                              [CUR_OPERATION_LINK_SID] = CASE WHEN @NextOperationLinkSid IS NULL THEN [CUR_OPERATION_LINK_SID] ELSE CONVERT(nvarchar(50), @NextOperationLinkSid) END,
                              [CUR_OPER_FIRST_IN_FLAG] = 'N',
                              [OPERATION_SID] = CASE WHEN @NextOperationSid IS NULL THEN [OPERATION_SID] ELSE @NextOperationSid END,
                              [OPERATION_SEQ] = CASE WHEN @NextOperationSeq IS NULL THEN [OPERATION_SEQ] ELSE @NextOperationSeq END
                          WHERE [LOT_SID] = @LotSid
                            AND [EDIT_TIME] = @OriginalEditTime;
                          """;
        cmd.Parameters.Add(new SqlParameter("@LotStatusSid", SqlDbType.Decimal) { Value = targetStatus.LOT_STATUS_SID });
        cmd.Parameters.Add(new SqlParameter("@LotStatusCode", SqlDbType.NVarChar, 50) { Value = targetStatus.LOT_STATUS_CODE });
        cmd.Parameters.Add(new SqlParameter("@LotSubStatusCode", SqlDbType.NVarChar, 50) { Value = (object?)lot.LOT_SUB_STATUS_CODE ?? DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@ReportTime", SqlDbType.DateTime) { Value = reportTime });
        cmd.Parameters.Add(new SqlParameter("@EditUser", SqlDbType.NVarChar, 50) { Value = editUser });
        cmd.Parameters.Add(new SqlParameter("@EditTime", SqlDbType.DateTime) { Value = editTime });
        cmd.Parameters.Add(new SqlParameter("@LotSid", SqlDbType.Decimal) { Value = lot.LOT_SID });
        cmd.Parameters.Add(new SqlParameter("@IsFinished", SqlDbType.Bit) { Value = nextRouteOperation == null });
        cmd.Parameters.Add(new SqlParameter("@NextOperationLinkSid", SqlDbType.Decimal) { Value = (object?)nextOperationLinkSid ?? DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@NextOperationSid", SqlDbType.Decimal) { Value = (object?)nextOperation?.WIP_OPERATION_SID ?? DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@NextOperationSeq", SqlDbType.Decimal) { Value = (object?)nextRouteOperation?.SEQ ?? DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@OriginalEditTime", SqlDbType.DateTime) { Value = lot.EDIT_TIME ?? lot.CREATE_TIME ?? reportTime });

        var affectedRows = await cmd.ExecuteNonQueryAsync(ct);
        if (affectedRows != 1)
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.Conflict, $"LOT check-out failed because the data was modified concurrently: {lot.LOT}");
    }

    private async Task EnsureLotNotExistsInTxAsync(SqlConnection conn, SqlTransaction tx, string lot, CancellationToken ct)
    {
        var where = new WhereBuilder<WipLotDto>()
            .AndEq(x => x.LOT, lot);

        var existing = await _sqlHelper.SelectFirstOrDefaultInTxAsync(conn, tx, where, ct: ct);
        if (existing != null)
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, $"The LOT already exists: {lot}");
    }

    private async Task<WipLotDto> GetLotByCodeInTxAsync(SqlConnection conn, SqlTransaction tx, string lotCode, CancellationToken ct)
    {
        var where = new WhereBuilder<WipLotDto>()
            .AndEq(x => x.LOT, lotCode);

        var lot = await _sqlHelper.SelectFirstOrDefaultInTxAsync(conn, tx, where, ct: ct);
        if (lot == null)
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, $"LOT not found: {lotCode}");

        return lot;
    }

    private async Task<AdmUserDto> GetUserByAccountAsync(string accountNo, CancellationToken ct)
    {
        var user = await _selectDtoService.SelectUserAsync(accountNo, ct);
        if (user == null)
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, $"User not found: {accountNo}");

        return user;
    }

    private async Task<WipWoDto> GetWorkOrderAsync(string wo, CancellationToken ct)
    {
        var workOrder = await _selectDtoService.SelectWorkOrderAsync(wo, ct);
        if (workOrder == null)
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, $"Work order not found: {wo}");

        if (string.IsNullOrWhiteSpace(workOrder.PART_NO))
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, $"Work order has no PART_NO: {wo}");

        return workOrder;
    }

    private async Task<WipRouteDto> GetRouteAsync(decimal routeSid, CancellationToken ct)
    {
        var where = new WhereBuilder<WipRouteDto>()
            .AndEq(x => x.WIP_ROUTE_SID, routeSid);

        var route = await _sqlHelper.SelectFirstOrDefaultAsync(where, ct);
        if (route == null)
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, $"Route not found: {routeSid}");

        return route;
    }

    private async Task<WipRouteOperationDto> GetCreateLotRouteOperationAsync(decimal routeSid, decimal? operationSid, CancellationToken ct)
    {
        var where = new WhereBuilder<WipRouteOperationDto>()
            .AndEq(x => x.WIP_ROUTE_SID, routeSid);

        var operations = await _sqlHelper.SelectWhereAsync(where, ct);
        var orderedOperations = operations.OrderBy(x => x.SEQ).ToList();
        if (orderedOperations.Count == 0)
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, $"Route has no operations: {routeSid}");

        if (operationSid == null)
            return orderedOperations[0];

        var routeOperation = orderedOperations.FirstOrDefault(x => x.WIP_OPERATION_SID == operationSid.Value);
        if (routeOperation == null)
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, $"Operation is not in route: ROUTE_SID={routeSid}, OPERATION_SID={operationSid}");

        return routeOperation;
    }

    private async Task<WipOperationDto> GetOperationBySidAsync(decimal operationSid, CancellationToken ct)
    {
        var where = new WhereBuilder<WipOperationDto>()
            .AndEq(x => x.WIP_OPERATION_SID, operationSid);

        var operation = await _sqlHelper.SelectFirstOrDefaultAsync(where, ct);
        if (operation == null)
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, $"Operation not found: {operationSid}");

        return operation;
    }

    private async Task<WipPartNoDto> GetPartByPartNoAsync(string partNo, CancellationToken ct)
    {
        var where = new WhereBuilder<WipPartNoDto>()
            .AndEq(x => x.WIP_PARTNO_NO, partNo);

        var part = await _sqlHelper.SelectFirstOrDefaultAsync(where, ct);
        if (part == null)
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, $"Part not found: {partNo}");

        return part;
    }

    private async Task<AdmReasonDto> GetReasonBySidAsync(decimal reasonSid, CancellationToken ct)
    {
        var where = new WhereBuilder<AdmReasonDto>()
            .AndEq(x => x.ADM_REASON_SID, reasonSid)
            .AndEq(x => x.ENABLE_FLAG, "Y");

        var reason = await _sqlHelper.SelectFirstOrDefaultAsync(where, ct);
        if (reason == null)
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, $"Reason not found or disabled: {reasonSid}");

        return reason;
    }

    private async Task<QmmDcItemDto> GetDcItemAsync(WipLotRecordDcItemInputDto item, CancellationToken ct)
    {
        if (item.DC_ITEM_SID.HasValue && item.DC_ITEM_SID.Value > 0)
        {
            var whereBySid = new WhereBuilder<QmmDcItemDto>()
                .AndEq(x => x.QMM_ITEM_SID, item.DC_ITEM_SID.Value)
                .AndEq(x => x.ENABLE_FLAG, "Y");

            var bySid = await _sqlHelper.SelectFirstOrDefaultAsync(whereBySid, ct);
            if (bySid != null)
                return bySid;
        }

        if (string.IsNullOrWhiteSpace(item.DC_ITEM_CODE))
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, "DC_ITEM_CODE is required.");

        var whereByCode = new WhereBuilder<QmmDcItemDto>()
            .AndEq(x => x.QMM_ITEM_NO, item.DC_ITEM_CODE)
            .AndEq(x => x.ENABLE_FLAG, "Y");

        var dcItem = await _sqlHelper.SelectFirstOrDefaultAsync(whereByCode, ct);
        if (dcItem == null)
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, $"DC item not found or disabled: {item.DC_ITEM_CODE}");

        return dcItem;
    }

    private async Task<EqmMasterDto> GetEquipmentByNoAsync(string eqpNo, CancellationToken ct)
    {
        var equipment = await _selectDtoService.SelectEquipmentAsync(eqpNo, ct);
        if (equipment == null)
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, $"Equipment not found: {eqpNo}");

        return equipment;
    }

    private async Task<WipLotStatusDto> GetLotStatusAsync(string statusCode, CancellationToken ct)
    {
        var where = new WhereBuilder<WipLotStatusDto>()
            .AndEq(x => x.LOT_STATUS_CODE, statusCode);

        var status = await _sqlHelper.SelectFirstOrDefaultAsync(where, ct);
        if (status == null)
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, $"Lot status not found: {statusCode}");

        return status;
    }

    private async Task<WipLotStatusDto> GetLotStatusBySidAsync(decimal statusSid, CancellationToken ct)
    {
        var where = new WhereBuilder<WipLotStatusDto>()
            .AndEq(x => x.LOT_STATUS_SID, statusSid);

        var status = await _sqlHelper.SelectFirstOrDefaultAsync(where, ct);
        if (status == null)
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, $"Lot status not found: {statusSid}");

        return status;
    }

    private async Task<DateTime> GetDbNowInTxAsync(SqlConnection conn, SqlTransaction tx, CancellationToken ct)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "SELECT SYSDATETIME();";
        var value = await cmd.ExecuteScalarAsync(ct);
        return value switch
        {
            DateTime dt => dt,
            _ => throw new InvalidOperationException("Failed to get database time.")
        };
    }

    private static decimal ParseOperationLinkSid(string? rawOperationLinkSid, string lotCode)
    {
        if (decimal.TryParse(rawOperationLinkSid, out var operationLinkSid))
            return operationLinkSid;

        throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, $"LOT has invalid CUR_OPERATION_LINK_SID: {lotCode}");
    }

    private async Task<int> GetNextLotHistorySequenceInTxAsync(SqlConnection conn, SqlTransaction tx, decimal lotSid, CancellationToken ct)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
                          SELECT ISNULL(MAX([SEQ]), 0) + 1
                          FROM [WIP_LOT_HIST]
                          WHERE [LOT_SID] = @LotSid;
                          """;
        cmd.Parameters.Add(new SqlParameter("@LotSid", SqlDbType.Decimal) { Value = lotSid });

        var value = await cmd.ExecuteScalarAsync(ct);
        return value switch
        {
            int seq => seq,
            decimal seq => (int)seq,
            long seq => (int)seq,
            _ => 1
        };
    }

    private async Task InsertLotCheckInUserRecordsInTxAsync(
        SqlConnection conn,
        SqlTransaction tx,
        WipLotCheckInInputDto input,
        WipLotDto lot,
        WipLotHistDto checkInHist,
        decimal operationLinkSid,
        DateTime createTime,
        decimal shiftSid,
        decimal workgroupSid,
        CancellationToken ct)
    {
        var lotCurUser = new WipLotCurUserDto
        {
            WIP_LOT_CUR_USER_SID = RandomHelper.GenerateRandomDecimal(),
            CUR_OPERATION_LINK_SID = operationLinkSid,
            LOT_SID = lot.LOT_SID,
            LOT = lot.LOT,
            CREATE_USER = input.ACCOUNT_NO,
            CREATE_TIME = createTime,
            DATA_LINK_SID = input.DATA_LINK_SID
        };

        var lotUserHist = new WipLotUserHistDto
        {
            WIP_LOT_USER_HIST_SID = RandomHelper.GenerateRandomDecimal(),
            IN_WIP_LOT_HIST_SID = checkInHist.WIP_LOT_HIST_SID,
            CREATE_USER = input.ACCOUNT_NO,
            USER_COMMENT = input.COMMENT,
            CREATE_IN_TIME = createTime,
            REPORT_IN_TIME = checkInHist.REPORT_TIME,
            OUT_FLAG = Flags.No,
            OUT_OK_QTY = 0,
            OUT_NG_QTY = 0,
            REPORT_OUT_OK_QTY = 0,
            REPORT_OUT_NG_QTY = 0,
            OPERATION_FINISH = Flags.OperationFinishNo,
            OPERATION_LINK_SID = operationLinkSid,
            SHIFT_SID = shiftSid,
            WORKGROUP_SID = workgroupSid,
            LOT_SUB_STATUS_CODE = input.LOT_SUB_STATUS_CODE
        };

        await _sqlHelper.InsertInTxAsync(conn, tx, lotCurUser, ct: ct);
        await _sqlHelper.InsertInTxAsync(conn, tx, lotUserHist, ct: ct);
    }

    private async Task InsertLotCheckInEquipmentRecordsInTxAsync(
        SqlConnection conn,
        SqlTransaction tx,
        WipLotCheckInInputDto input,
        WipLotDto lot,
        WipLotHistDto checkInHist,
        EqmMasterDto equipment,
        decimal operationLinkSid,
        DateTime reportTime,
        CancellationToken ct)
    {
        var curEqpWhere = new WhereBuilder<WipLotCurEqpDto>()
            .AndEq(x => x.LOT_SID, lot.LOT_SID)
            .AndEq(x => x.EQP_SID, equipment.EQM_MASTER_SID);

        var existingCurrentEquipment = await _sqlHelper.SelectFirstOrDefaultInTxAsync(conn, tx, curEqpWhere, ct: ct);
        if (existingCurrentEquipment == null)
        {
            var lotCurEqp = new WipLotCurEqpDto
            {
                WIP_LOT_CUR_EQP_SID = RandomHelper.GenerateRandomDecimal(),
                OPERATION_LINK_SID = operationLinkSid,
                LOT_SID = lot.LOT_SID,
                LOT = lot.LOT,
                EQP_SID = equipment.EQM_MASTER_SID,
                EQP_NO = equipment.EQM_MASTER_NO,
                CREATE_TIME = reportTime,
                DATA_LINK_SID = input.DATA_LINK_SID
            };

            await _sqlHelper.InsertInTxAsync(conn, tx, lotCurEqp, ct: ct);
        }

        var lotEqpHist = new WipLotEqpHistDto
        {
            WIP_LOT_EQP_HIST_SID = RandomHelper.GenerateRandomDecimal(),
            WIP_LOT_HIST_SID = checkInHist.WIP_LOT_HIST_SID,
            EQP_SID = equipment.EQM_MASTER_SID,
            EQP_NO = equipment.EQM_MASTER_NO,
            EQP_NAME = equipment.EQM_MASTER_NAME,
            EQP_COMMENT = input.COMMENT,
            CREATE_TIME = reportTime
        };

        await _sqlHelper.InsertInTxAsync(conn, tx, lotEqpHist, ct: ct);
    }

    private static async Task UpdateLotCheckInStateInTxAsync(
        SqlConnection conn,
        SqlTransaction tx,
        WipLotCheckInInputDto input,
        WipLotDto lot,
        WipLotStatusDto runStatus,
        DateTime reportTime,
        DateTime createTime,
        bool isFirstCheckIn,
        CancellationToken ct)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
                          UPDATE [WIP_LOT]
                          SET [CUR_OPER_BATCH_ID] = @DataLinkSid,
                              [LOT_STATUS_SID] = @LotStatusSid,
                              [LOT_STATUS_CODE] = @LotStatusCode,
                              [LOT_SUB_STATUS_CODE] = @LotSubStatusCode,
                              [LAST_STATUS_CHANGE_TIME] = @ReportTime,
                              [LAST_TRANS_TIME] = @CreateTime,
                              [EDIT_USER] = @EditUser,
                              [EDIT_TIME] = @EditTime,
                              [CUR_OPER_FIRST_IN_FLAG] = CASE WHEN @IsFirstCheckIn = 1 THEN 'Y' ELSE [CUR_OPER_FIRST_IN_FLAG] END,
                              [CUR_OPER_FIRST_IN_TIME] = CASE WHEN @IsFirstCheckIn = 1 THEN @ReportTime ELSE [CUR_OPER_FIRST_IN_TIME] END
                          WHERE [LOT_SID] = @LotSid
                            AND [EDIT_TIME] = @OriginalEditTime;
                          """;

        cmd.Parameters.Add(new SqlParameter("@DataLinkSid", SqlDbType.Decimal) { Value = input.DATA_LINK_SID });
        cmd.Parameters.Add(new SqlParameter("@LotStatusSid", SqlDbType.Decimal) { Value = runStatus.LOT_STATUS_SID });
        cmd.Parameters.Add(new SqlParameter("@LotStatusCode", SqlDbType.NVarChar, 50) { Value = runStatus.LOT_STATUS_CODE });
        cmd.Parameters.Add(new SqlParameter("@LotSubStatusCode", SqlDbType.NVarChar, 50) { Value = (object?)input.LOT_SUB_STATUS_CODE ?? DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@ReportTime", SqlDbType.DateTime) { Value = reportTime });
        cmd.Parameters.Add(new SqlParameter("@CreateTime", SqlDbType.DateTime) { Value = createTime });
        cmd.Parameters.Add(new SqlParameter("@EditUser", SqlDbType.NVarChar, 50) { Value = input.ACCOUNT_NO });
        cmd.Parameters.Add(new SqlParameter("@EditTime", SqlDbType.DateTime) { Value = createTime });
        cmd.Parameters.Add(new SqlParameter("@IsFirstCheckIn", SqlDbType.Bit) { Value = isFirstCheckIn });
        cmd.Parameters.Add(new SqlParameter("@LotSid", SqlDbType.Decimal) { Value = lot.LOT_SID });
        cmd.Parameters.Add(new SqlParameter("@OriginalEditTime", SqlDbType.DateTime) { Value = lot.EDIT_TIME ?? lot.CREATE_TIME ?? reportTime });

        var affectedRows = await cmd.ExecuteNonQueryAsync(ct);
        if (affectedRows != 1)
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.Conflict, $"LOT check-in failed because the data was modified concurrently: {lot.LOT}");
    }
}

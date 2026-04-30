using System.Data;
using DbExtensions;
using DcMateClassLibrary.Helper;
using DcMateClassLibrary.Helper.HttpHelper;
using DcMateH5.Abstractions.Wip;
using DcMateH5Api.Areas.Wip.Model;
using Microsoft.Data.SqlClient;

namespace DcMateH5.Infrastructure.Wip;

public class WipBaseSettingService : IWipBaseSettingService
{
    private static class Flags
    {
        public const string Yes = "Y";
        public const string No = "N";
        public const string Enabled = "Y";
    }

    private readonly SQLGenerateHelper _sqlHelper;
    private readonly IBaseInfoCheckExistService _baseInfoCheckExistService;
    private readonly ISelectDtoService _selectDtoService;

    public WipBaseSettingService(
        SQLGenerateHelper sqlHelper,
        IBaseInfoCheckExistService baseInfoCheckExistService,
        ISelectDtoService selectDtoService)
    {
        _sqlHelper = sqlHelper;
        _baseInfoCheckExistService = baseInfoCheckExistService;
        _selectDtoService = selectDtoService;
    }

    public async Task<decimal> CheckInAsync(WipCheckInInputDto input, CancellationToken ct = default)
    {
        return await _sqlHelper.TxAsync(
            async (conn, tx, innerCt) =>
            {
                return await CreateCheckInInTxAsync(conn, tx, input, innerCt);
            },
            isolation: IsolationLevel.ReadCommitted,
            ct: ct);
    }

    public async Task CheckInCancelAsync(WipCheckInCancelInputDto input, CancellationToken ct = default)
    {
        await _sqlHelper.TxAsync(
            async (conn, tx, innerCt) =>
            {
                await EnsureHistCancelableAsync(conn, tx, input.WIP_OPI_WDOEACICO_HIST_SID, innerCt);
                await DeleteCheckInGraphAsync(conn, tx, input.WIP_OPI_WDOEACICO_HIST_SID, innerCt);
            },
            isolation: IsolationLevel.ReadCommitted,
            ct: ct);
    }

    public async Task AddDetailsAsync(WipAddDetailInputDto input, CancellationToken ct = default)
    {
        await _sqlHelper.TxAsync(
            async (conn, tx, innerCt) =>
            {
                await AddDetailsInTxAsync(conn, tx, input.WIP_OPI_WDOEACICO_HIST_SID, ToCombineInput(input), innerCt);
            },
            isolation: IsolationLevel.ReadCommitted,
            ct: ct);
    }

    public async Task EditDetailsAsync(WipEditDetailInputDto input, CancellationToken ct = default)
    {
        await _sqlHelper.TxAsync(
            async (conn, tx, innerCt) =>
            {
                await EditDetailsInTxAsync(conn, tx, input, innerCt);
            },
            isolation: IsolationLevel.ReadCommitted,
            ct: ct);
    }

    public async Task DeleteDetailsAsync(WipDeleteDetailInputDto input, CancellationToken ct = default)
    {
        await _sqlHelper.TxAsync(
            async (conn, tx, innerCt) =>
            {
                await DeleteDetailsInTxAsync(conn, tx, input.WIP_OPI_WDOEACICO_HIST_DETAIL_SID, innerCt);
            },
            isolation: IsolationLevel.ReadCommitted,
            ct: ct);
    }

    public async Task CheckOutAsync(WipCheckOutInputDto input, CancellationToken ct = default)
    {
        await _sqlHelper.TxAsync(
            async (conn, tx, innerCt) =>
            {
                await CheckOutInTxAsync(conn, tx, input.WIP_OPI_WDOEACICO_HIST_SID, input.CHECK_OUT_TIME, innerCt);
            },
            isolation: IsolationLevel.ReadCommitted,
            ct: ct);
    }

    /// <summary>
    /// 一次做完 Check in、 Add WIP_OPI_WDOEACICO_HIST_DETAIL、Check out
    /// </summary>
    /// <param name="input"></param>
    /// <param name="ct"></param>
    public async Task CheckInAddDetailsCheckOutAsync(WipCheckInAddDetailsCheckOutInputDto input, CancellationToken ct = default)
    {
        await _sqlHelper.TxAsync(
            async (conn, tx, innerCt) =>
            {
                var histSid = await CreateCheckInInTxAsync(conn, tx, input.CheckIn, innerCt);

                await AddDetailsInTxAsync(
                    conn,
                    tx,
                    histSid,
                    input.AddDetails,
                    innerCt);

                await CheckOutInTxAsync(conn, tx, histSid, input.CHECK_OUT_TIME, innerCt);
            },
            isolation: IsolationLevel.ReadCommitted,
            ct: ct);
    }

    private static WipAddDetailForCombineInputDto ToCombineInput(WipAddDetailInputDto input)
    {
        return new WipAddDetailForCombineInputDto
        {
            OK_QTY = input.OK_QTY,
            NG_QTY = input.NG_QTY,
            COMMENT = input.COMMENT,
            NgDetails = input.NgDetails
        };
    }

    /// <summary>
    /// 進站
    /// </summary>
    /// <param name="conn"></param>
    /// <param name="tx"></param>
    /// <param name="input"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    private async Task<decimal> CreateCheckInInTxAsync(SqlConnection conn, SqlTransaction tx, WipCheckInInputDto input, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var userSids = await ValidateAndGetUserSidsAsync(input, ct);
        await ValidateEquipmentAsync(input, ct);
        await ValidateWorkOrderAsync(input, ct);
        await ValidateOperationAsync(input, ct);
        await ValidateDepartmentAsync(input, ct);

        await EnsureNoDuplicateCheckInInTxAsync(conn, tx, input, ct);

        var histSid = RandomHelper.GenerateRandomDecimal();

        var hist = new WipOpiWdoeacicoHistDto
        {
            WIP_OPI_WDOEACICO_HIST_SID = histSid,
            WO = input.WorkOrder,
            CHECK_IN_TIME = input.CheckInTime,
            OPERATION_CODE = input.Operation,
            DEPT_NO = input.Department,
            COMMENT = input.Comment,
            COMPLETED = Flags.No
        };

        if (input.Equipment != null && input.Equipment.Count == 1)
        {
            hist.EQP_NO = input.Equipment[0];
        }

        await _sqlHelper.InsertInTxAsync(conn, tx, hist, ct: ct);

        await InsertEquipmentsIfNeededInTxAsync(conn, tx, histSid, input.Equipment, ct);
        await InsertUsersIfNeededInTxAsync(conn, tx, histSid, input.Account, userSids, ct);

        return histSid;
    }

    /// <summary>
    /// 機台歷史紀錄
    /// </summary>
    /// <param name="conn"></param>
    /// <param name="tx"></param>
    /// <param name="histSid"></param>
    /// <param name="equipments"></param>
    /// <param name="ct"></param>
    private async Task InsertEquipmentsIfNeededInTxAsync(
        SqlConnection conn,
        SqlTransaction tx,
        decimal histSid,
        IReadOnlyList<string>? equipments,
        CancellationToken ct)
    {
        if (equipments == null || equipments.Count <= 1)
        {
            return;
        }

        foreach (var eqpNo in equipments)
        {
            var histEqp = new WipOpiWdoeacicoHistEqpDto
            {
                WIP_OPI_WDOEACICO_HIST_EQP_SID = RandomHelper.GenerateRandomDecimal(),
                WIP_OPI_WDOEACICO_HIST_SID = histSid,
                EQP_NO = eqpNo,
                ENABLE_FLAG = Flags.Enabled
            };

            await _sqlHelper.InsertInTxAsync(conn, tx, histEqp, ct: ct);
        }
    }

    /// <summary>
    /// 人員歷史紀錄
    /// </summary>
    /// <param name="conn"></param>
    /// <param name="tx"></param>
    /// <param name="histSid"></param>
    /// <param name="accounts"></param>
    /// <param name="userSids"></param>
    /// <param name="ct"></param>
    private async Task InsertUsersIfNeededInTxAsync(
        SqlConnection conn,
        SqlTransaction tx,
        decimal histSid,
        IReadOnlyList<string>? accounts,
        IReadOnlyList<Guid> userSids,
        CancellationToken ct)
    {
        if (accounts == null || accounts.Count == 0)
        {
            return;
        }

        for (var i = 0; i < accounts.Count; i++)
        {
            var histUser = new WipOpiWdoeacicoHistUserDto
            {
                WIP_OPI_WDOEACICO_HIST_USER_SID = RandomHelper.GenerateRandomDecimal(),
                WIP_OPI_WDOEACICO_HIST_SID = histSid,
                ADM_USER_SID = userSids[i],
                ACCOUNT_NO = accounts[i]
            };

            await _sqlHelper.InsertInTxAsync(conn, tx, histUser, ct: ct);
        }
    }

    /// <summary>
    /// 新增進站明細資料，並同步維護不良原因明細與主表累計數量
    /// </summary>
    /// <param name="conn">目前交易使用的資料庫連線</param>
    /// <param name="tx">目前交易物件</param>
    /// <param name="histSid">進站主檔 SID（WIP_OPI_WDOEACICO_HIST_SID）</param>
    /// <param name="input">明細輸入資料，包含良品數、不良品數、備註與不良原因清單</param>
    /// <param name="ct">取消權杖</param>
    /// <returns></returns>
    /// <remarks>
    /// ### 處理流程
    ///
    /// 1. 建立一筆新的 `WIP_OPI_WDOEACICO_HIST_DETAIL` 明細資料
    /// 2. 將 `NgDetails` 中的不良原因明細寫入 `WIP_OPI_WDOEACICO_HIST_NG_REASON_DETAIL`
    /// 3. 重新計算指定 `histSid` 底下所有明細的 `OK_QTY` 與 `NG_QTY` 總和
    /// 4. 將累計結果回寫至主表 `WIP_OPI_WDOEACICO_HIST` 的 `TOTAL_OK_QTY` 與 `TOTAL_NG_QTY`
    ///
    /// ### 注意事項
    ///
    /// - 此方法僅負責新增明細，不會檢查主檔是否已 checkout 或 completed，呼叫端需先保證業務狀態合法
    /// - `NG_REASON_QTY` 會依 `NgDetails` 的 `NG_QTY` 加總後自動計算
    /// - 當 `NgDetails` 為 `null` 或空集合時，不會新增不良原因明細
    /// </remarks>
    private async Task AddDetailsInTxAsync(
        SqlConnection conn,
        SqlTransaction tx,
        decimal histSid,
        WipAddDetailForCombineInputDto input,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var detailSid = RandomHelper.GenerateRandomDecimal();

        var detail = new WipOpiWdoeacicoHistDetailDto
        {
            WIP_OPI_WDOEACICO_HIST_DETAIL_SID = detailSid,
            WIP_OPI_WDOEACICO_HIST_SID = histSid,
            OK_QTY = input.OK_QTY,
            NG_QTY = input.NG_QTY,
            NG_REASON_QTY = input.NgDetails?.Sum(x => x.NG_QTY) ?? 0,
            COMMENT = input.COMMENT ?? string.Empty
        };

        await _sqlHelper.InsertInTxAsync(conn, tx, detail, ct: ct);

        await UpsertNgReasonDetailsInTxAsync(conn, tx, detailSid, input.NgDetails, ct);

        await RecalculateAndUpdateHistTotalQtyInTxAsync(conn, tx, histSid, ct);
    }

    private async Task EditDetailsInTxAsync(SqlConnection conn, SqlTransaction tx, WipEditDetailInputDto input, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var detailSid = input.WIP_OPI_WDOEACICO_HIST_DETAIL_SID;

        await _sqlHelper.UpdateById<WipOpiWdoeacicoHistDetailDto>(detailSid)
            .Set(x => x.OK_QTY, input.OK_QTY)
            .Set(x => x.NG_QTY, input.NG_QTY)
            .Set(x => x.NG_REASON_QTY, input.NgDetails?.Sum(x => x.NG_QTY) ?? 0)
            .Set(x => x.COMMENT, input.COMMENT ?? string.Empty)
            .ExecuteInTxAsync(conn, tx, ct: ct);

        await DeleteNgReasonDetailsByDetailSidInTxAsync(conn, tx, detailSid, ct);
        await UpsertNgReasonDetailsInTxAsync(conn, tx, detailSid, input.NgDetails, ct);

        await RecalculateAndUpdateHistTotalQtyInTxAsync(conn, tx, input.WIP_OPI_WDOEACICO_HIST_SID, ct);
    }

    private async Task DeleteDetailsInTxAsync(SqlConnection conn, SqlTransaction tx, decimal detailSid, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var detailWhere = new WhereBuilder<WipOpiWdoeacicoHistDetailDto>()
            .AndEq(x => x.WIP_OPI_WDOEACICO_HIST_DETAIL_SID!, detailSid);

        var detail = await _sqlHelper.SelectFirstOrDefaultInTxAsync(conn, tx, detailWhere, ct: ct);
        if (detail == null)
        {
            throw new HttpStatusCodeException(
                System.Net.HttpStatusCode.BadRequest,
                $"Hist detail SID {detailSid} does not exist.");
        }

        await DeleteNgReasonDetailsByDetailSidInTxAsync(conn, tx, detailSid, ct);
        await _sqlHelper.DeleteWhereInTxAsync(conn, tx, detailWhere, ct: ct);

        await RecalculateAndUpdateHistTotalQtyInTxAsync(conn, tx, detail.WIP_OPI_WDOEACICO_HIST_SID, ct);
    }

    private async Task CheckOutInTxAsync(SqlConnection conn, SqlTransaction tx, decimal histSid, DateTime checkOutTime, CancellationToken ct)
    {
        await _sqlHelper.UpdateById<WipOpiWdoeacicoHistDto>(histSid)
            .Set(x => x.CHECK_OUT_TIME!, checkOutTime)
            .Set(x => x.COMPLETED!, Flags.Yes)
            .ExecuteInTxAsync(conn, tx, ct: ct);
    }

    private async Task RecalculateAndUpdateHistTotalQtyInTxAsync(SqlConnection conn, SqlTransaction tx, decimal histSid, CancellationToken ct)
    {
        var (totalOkQty, totalNgQty) = await CalculateTotalQtyInTxAsync(conn, tx, histSid, ct);

        await _sqlHelper.UpdateById<WipOpiWdoeacicoHistDto>(histSid)
            .Set(x => x.TOTAL_OK_QTY!, totalOkQty)
            .Set(x => x.TOTAL_NG_QTY!, totalNgQty)
            .ExecuteInTxAsync(conn, tx, ct: ct);
    }

    private async Task<(decimal totalOkQty, decimal totalNgQty)> CalculateTotalQtyInTxAsync(
        SqlConnection conn,
        SqlTransaction tx,
        decimal histSid,
        CancellationToken ct)
    {
        var where = new WhereBuilder<WipOpiWdoeacicoHistDetailDto>()
            .AndEq(x => x.WIP_OPI_WDOEACICO_HIST_SID!, histSid);

        var rows = await _sqlHelper.SelectWhereInTxAsync(conn, tx, where, ct: ct);

        var totalOk = rows.Select(x => x.OK_QTY).Sum();
        var totalNg = rows.Select(x => x.NG_QTY).Sum();

        return (totalOk, totalNg);
    }

    private async Task UpsertNgReasonDetailsInTxAsync(
        SqlConnection conn,
        SqlTransaction tx,
        decimal detailSid,
        IReadOnlyList<NgDetailItem>? ngDetails,
        CancellationToken ct)
    {
        if (ngDetails == null || ngDetails.Count == 0)
        {
            return;
        }

        foreach (var ng in ngDetails)
        {
            var entity = new WipOpiWdoeacicoHistNgReasonDetailDto
            {
                WIP_OPI_WDOEACICO_HIST_NG_REASON_DETAIL_SID = RandomHelper.GenerateRandomDecimal(),
                WIP_OPI_WDOEACICO_HIST_DETAIL_SID = detailSid,
                NG_QTY = ng.NG_QTY,
                NG_CODE = ng.NG_CODE,
                COMMENT = ng.Comment,
                ENABLE_FLAG = Flags.Enabled
            };

            await _sqlHelper.InsertInTxAsync(conn, tx, entity, ct: ct);
        }
    }

    private async Task DeleteNgReasonDetailsByDetailSidInTxAsync(SqlConnection conn, SqlTransaction tx, decimal detailSid, CancellationToken ct)
    {
        var where = new WhereBuilder<WipOpiWdoeacicoHistNgReasonDetailDto>()
            .AndEq(x => x.WIP_OPI_WDOEACICO_HIST_DETAIL_SID!, detailSid);

        await _sqlHelper.DeleteWhereInTxAsync(conn, tx, where, ct: ct);
    }

    /// <summary>
    /// 防止相同工單、機台、進站時間 重複進站
    /// </summary>
    /// <param name="conn"></param>
    /// <param name="tx"></param>
    /// <param name="input"></param>
    /// <param name="ct"></param>
    /// <exception cref="HttpStatusCodeException"></exception>
    private async Task EnsureNoDuplicateCheckInInTxAsync(SqlConnection conn, SqlTransaction tx, WipCheckInInputDto input, CancellationToken ct)
    {
        if (input.Equipment != null && input.Equipment.Any())
        {
            foreach (var eqpNo in input.Equipment)
            {
                var dupWhere = new WhereBuilder<WipOpiWdoeacicoHistDto>()
                    .AndEq(x => x.WO!, input.WorkOrder)
                    .AndEq(x => x.EQP_NO!, eqpNo)
                    .AndEq(x => x.CHECK_IN_TIME!, input.CheckInTime);

                var dup = await _sqlHelper.SelectFirstOrDefaultInTxAsync(conn, tx, dupWhere, ct: ct);
                if (dup != null)
                {
                    throw new HttpStatusCodeException(
                        System.Net.HttpStatusCode.BadRequest,
                        $"Duplicate check-in found for WO: {input.WorkOrder}, Equipment: {eqpNo} at Time: {input.CheckInTime}");
                }
            }

            return;
        }

        var dupWhereNoEqp = new WhereBuilder<WipOpiWdoeacicoHistDto>()
            .AndEq(x => x.WO!, input.WorkOrder)
            .AndEq(x => x.CHECK_IN_TIME!, input.CheckInTime);

        var duplicate = await _sqlHelper.SelectFirstOrDefaultInTxAsync(conn, tx, dupWhereNoEqp, ct: ct);
        if (duplicate != null && string.IsNullOrEmpty(duplicate.EQP_NO))
        {
            throw new HttpStatusCodeException(
                System.Net.HttpStatusCode.BadRequest,
                $"Duplicate check-in found for WO: {input.WorkOrder} at Time: {input.CheckInTime} (No Equipment)");
        }
    }

    /// <summary>
    /// 檢查使用者帳號是否存在
    /// </summary>
    /// <param name="input"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    /// <exception cref="HttpStatusCodeException"></exception>
    private async Task<List<Guid>> ValidateAndGetUserSidsAsync(WipCheckInInputDto input, CancellationToken ct)
    {
        var userSids = new List<Guid>();

        if (input.Account == null || input.Account.Count == 0)
        {
            return userSids;
        }

        foreach (var accountNo in input.Account)
        {
            var user = await _baseInfoCheckExistService.CheckUserExistAsync(accountNo, ct);
            if (user == null)
            {
                throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, $"Account {accountNo} does not exist.");
            }

            userSids.Add(user.USER_SID);
        }

        return userSids;
    }

    /// <summary>
    /// 檢查機台是否存在
    /// </summary>
    /// <param name="input"></param>
    /// <param name="ct"></param>
    /// <exception cref="HttpStatusCodeException"></exception>
    private async Task ValidateEquipmentAsync(WipCheckInInputDto input, CancellationToken ct)
    {
        if (input.Equipment == null || input.Equipment.Count == 0)
        {
            return;
        }

        foreach (var eqpNo in input.Equipment)
        {
            var eqp = await _baseInfoCheckExistService.CheckEquipmentExistAsync(eqpNo, ct);
            if (eqp == null)
            {
                throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, $"Equipment {eqpNo} does not exist.");
            }
        }
    }

    /// <summary>
    /// 檢查工單否存在
    /// </summary>
    /// <param name="input"></param>
    /// <param name="ct"></param>
    /// <exception cref="HttpStatusCodeException"></exception>
    private async Task ValidateWorkOrderAsync(WipCheckInInputDto input, CancellationToken ct)
    {
        var wo = await _baseInfoCheckExistService.CheckWorkOrderExistAsync(input.WorkOrder, ct);
        if (wo == null)
        {
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, $"Work Order {input.WorkOrder} does not exist.");
        }
    }

    /// <summary>
    /// 檢查工作站是否存在
    /// </summary>
    /// <param name="input"></param>
    /// <param name="ct"></param>
    /// <exception cref="HttpStatusCodeException"></exception>
    private async Task ValidateOperationAsync(WipCheckInInputDto input, CancellationToken ct)
    {
        var operation = await _baseInfoCheckExistService.CheckOperationExistAsync(input.Operation, ct);
        if (operation == null)
        {
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, $"Operation {input.Operation} does not exist.");
        }
    }

    /// <summary>
    /// 檢查部門是否存在
    /// </summary>
    /// <param name="input"></param>
    /// <param name="ct"></param>
    /// <exception cref="HttpStatusCodeException"></exception>
    private async Task ValidateDepartmentAsync(WipCheckInInputDto input, CancellationToken ct)
    {
        var dept = await _baseInfoCheckExistService.CheckDepartmentExistAsync(input.Department, ct);
        if (dept == null)
        {
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, $"Department {input.Department} does not exist.");
        }
    }

    private async Task EnsureHistCancelableAsync(SqlConnection conn, SqlTransaction tx, decimal histSid, CancellationToken ct)
    {
        var where = new WhereBuilder<WipOpiWdoeacicoHistDto>()
            .AndEq(x => x.WIP_OPI_WDOEACICO_HIST_SID!, histSid);

        var hist = await _sqlHelper.SelectFirstOrDefaultInTxAsync(conn, tx, where, ct: ct);
        if (hist == null)
        {
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, $"Hist SID {histSid} does not exist.");
        }

        if (string.Equals(hist.COMPLETED, Flags.Yes, StringComparison.OrdinalIgnoreCase))
        {
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, $"Hist SID {histSid} is completed and cannot be canceled.");
        }

        if (hist.CHECK_OUT_TIME.HasValue)
        {
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, $"Hist SID {histSid} has checkout time and cannot be canceled.");
        }
    }

    private async Task DeleteCheckInGraphAsync(SqlConnection conn, SqlTransaction tx, decimal histSid, CancellationToken ct)
    {
        var detailWhere = new WhereBuilder<WipOpiWdoeacicoHistDetailDto>()
            .AndEq(x => x.WIP_OPI_WDOEACICO_HIST_SID!, histSid);

        var details = await _sqlHelper.SelectWhereInTxAsync(conn, tx, detailWhere, ct: ct);
        var detailSids = details.Select(x => x.WIP_OPI_WDOEACICO_HIST_DETAIL_SID).ToList();

        await DeleteNgReasonDetailsInTxAsync(conn, tx, detailSids, ct);

        await _sqlHelper.DeleteWhereInTxAsync(conn, tx, detailWhere, ct: ct);

        var userWhere = new WhereBuilder<WipOpiWdoeacicoHistUserDto>()
            .AndEq(x => x.WIP_OPI_WDOEACICO_HIST_SID!, histSid);
        await _sqlHelper.DeleteWhereInTxAsync(conn, tx, userWhere, ct: ct);

        var eqpWhere = new WhereBuilder<WipOpiWdoeacicoHistEqpDto>()
            .AndEq(x => x.WIP_OPI_WDOEACICO_HIST_SID!, histSid);
        await _sqlHelper.DeleteWhereInTxAsync(conn, tx, eqpWhere, ct: ct);

        var histWhere = new WhereBuilder<WipOpiWdoeacicoHistDto>()
            .AndEq(x => x.WIP_OPI_WDOEACICO_HIST_SID!, histSid);
        await _sqlHelper.DeleteWhereInTxAsync(conn, tx, histWhere, ct: ct);
    }

    private async Task DeleteNgReasonDetailsInTxAsync(
        SqlConnection conn,
        SqlTransaction tx,
        IReadOnlyList<decimal> detailSids,
        CancellationToken ct)
    {
        if (detailSids.Count == 0)
        {
            return;
        }

        foreach (var detailSid in detailSids)
        {
            var where = new WhereBuilder<WipOpiWdoeacicoHistNgReasonDetailDto>()
                .AndEq(x => x.WIP_OPI_WDOEACICO_HIST_DETAIL_SID!, detailSid);

            await _sqlHelper.DeleteWhereInTxAsync(conn, tx, where, ct: ct);
        }
    }
}

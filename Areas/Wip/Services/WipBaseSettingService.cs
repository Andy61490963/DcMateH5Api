using System.Data;
using DcMateH5Api.SqlHelper;
using DcMateH5Api.Areas.Wip.Interfaces;
using DcMateH5Api.Areas.Wip.Model;
using DcMateH5Api.Helper;
using Microsoft.Data.SqlClient;

namespace DcMateH5Api.Areas.Wip.Services
{
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

        public async Task CheckInAsync(WipCheckInInputDto input, CancellationToken ct = default)
        {
            await _sqlHelper.TxAsync(
                async (conn, tx, innerCt) =>
                {
                    await CreateCheckInInTxAsync(conn, tx, input, innerCt);
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

        private async Task InsertUsersIfNeededInTxAsync(
            SqlConnection conn,
            SqlTransaction tx,
            decimal histSid,
            IReadOnlyList<string>? accounts,
            IReadOnlyList<decimal> userSids,
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
                    UMM_USER_SID = userSids[i],
                    ACCOUNT_NO = accounts[i]
                };

                await _sqlHelper.InsertInTxAsync(conn, tx, histUser, ct: ct);
            }
        }

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

        private async Task<List<decimal>> ValidateAndGetUserSidsAsync(WipCheckInInputDto input, CancellationToken ct)
        {
            var userSids = new List<decimal>();

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

        private async Task ValidateWorkOrderAsync(WipCheckInInputDto input, CancellationToken ct)
        {
            var wo = await _baseInfoCheckExistService.CheckWorkOrderExistAsync(input.WorkOrder, ct);
            if (wo == null)
            {
                throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, $"Work Order {input.WorkOrder} does not exist.");
            }
        }

        private async Task ValidateOperationAsync(WipCheckInInputDto input, CancellationToken ct)
        {
            var operation = await _baseInfoCheckExistService.CheckOperationExistAsync(input.Operation, ct);
            if (operation == null)
            {
                throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, $"Operation {input.Operation} does not exist.");
            }
        }

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
}

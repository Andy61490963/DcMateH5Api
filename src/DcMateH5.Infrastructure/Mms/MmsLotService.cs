using System.Data;
using DbExtensions;
using DcMateClassLibrary.Helper;
using DcMateClassLibrary.Helper.HttpHelper;
using DcMateH5.Abstractions.Mms;
using DcMateH5.Abstractions.Mms.Models;
using DcMateH5Api.Models;
using Microsoft.Data.SqlClient;

namespace DcMateH5.Infrastructure.Mms;

public class MmsLotService : IMmsLotService
{
    private static class MLotCodes
    {
        public const string Create = "MLOT_CREATE";
        public const string Consume = "MLOT_CONSUME";
        public const string UnConsume = "MLOT_UNCONSUME";
        public const string StateChange = "MLOT_STATE_CHANGE";
        public const string WaitStatus = "Wait";
        public const string FinishedStatus = "Finished";
    }

    private readonly SQLGenerateHelper _sqlHelper;

    public MmsLotService(SQLGenerateHelper sqlHelper)
    {
        _sqlHelper = sqlHelper;
    }

    public async Task<Result<bool>> CreateMLotAsync(MmsCreateMLotInputDto input, CancellationToken ct = default)
    {
        await _sqlHelper.TxAsync(
            async (conn, tx, innerCt) => await CreateMLotInTxAsync(conn, tx, input, innerCt),
            isolation: IsolationLevel.ReadCommitted,
            ct: ct);

        return Result<bool>.Ok(true);
    }

    public async Task<Result<bool>> MLotConsumeAsync(MmsMLotConsumeInputDto input, CancellationToken ct = default)
    {
        await _sqlHelper.TxAsync(
            async (conn, tx, innerCt) => await MLotConsumeInTxAsync(conn, tx, input, innerCt),
            isolation: IsolationLevel.ReadCommitted,
            ct: ct);

        return Result<bool>.Ok(true);
    }

    public async Task<Result<bool>> MLotUNConsumeAsync(MmsMLotUNConsumeInputDto input, CancellationToken ct = default)
    {
        await _sqlHelper.TxAsync(
            async (conn, tx, innerCt) => await MLotUNConsumeInTxAsync(conn, tx, input, innerCt),
            isolation: IsolationLevel.ReadCommitted,
            ct: ct);

        return Result<bool>.Ok(true);
    }

    public async Task<Result<bool>> MLotStateChangeAsync(MmsMLotStateChangeInputDto input, CancellationToken ct = default)
    {
        await _sqlHelper.TxAsync(
            async (conn, tx, innerCt) => await MLotStateChangeInTxAsync(conn, tx, input, innerCt),
            isolation: IsolationLevel.ReadCommitted,
            ct: ct);

        return Result<bool>.Ok(true);
    }

    /// <summary>
    /// 建立物料批。舊版 CreateMLot 會同時建立 MMS_MLOT 主檔與 MLOT_CREATE 履歷。
    /// 新版固定以 Wait 作為建立後狀態，前端不需要傳 MLOT_STATUS_CODE。
    /// </summary>
    /// <remarks>
    /// 建立 MLOT 庫存：新增 MMS_MLOT 主檔與 MLOT_CREATE 履歷，新建狀態固定為 Wait。
    /// </remarks>
    private async Task CreateMLotInTxAsync(SqlConnection conn, SqlTransaction tx, MmsCreateMLotInputDto input, CancellationToken ct)
    {
        ValidateCreateMLotInput(input);
        await EnsureUserExistsAsync(conn, tx, input.ACCOUNT_NO, ct);
        await EnsureMlotStatusExistsAsync(conn, tx, MLotCodes.WaitStatus, ct);
        await EnsureMlotNotExistsAsync(conn, tx, input.MLOT, ct);

        var now = await GetDbNowInTxAsync(conn, tx, ct);
        var reportTime = input.REPORT_TIME ?? now;
        var histSid = RandomHelper.GenerateRandomDecimal();
        var mlotSid = RandomHelper.GenerateRandomDecimal();
        var parentMlot = string.IsNullOrWhiteSpace(input.PARENT_MLOT) ? input.MLOT : input.PARENT_MLOT.Trim();
        var mlotType = string.IsNullOrWhiteSpace(input.MLOT_TYPE) ? "N" : input.MLOT_TYPE.Trim();

        await InsertMlotAsync(
            conn,
            tx,
            new MLotInsert(
                mlotSid,
                input.MLOT.Trim(),
                mlotType,
                input.MLOT_WO,
                input.PART_NO.Trim(),
                input.MLOT_QTY,
                parentMlot,
                MLotCodes.WaitStatus,
                input.EXPIRY_DATE,
                input.ALIAS_MLOT1,
                input.ALIAS_MLOT2,
                input.DATE_CODE,
                histSid,
                input.COMMENT,
                input.ACCOUNT_NO.Trim(),
                now),
            ct);

        await InsertMlotHistAsync(
            conn,
            tx,
            new MLotHistInsert(
                histSid,
                input.DATA_LINK_SID,
                mlotSid,
                input.MLOT.Trim(),
                input.ALIAS_MLOT1,
                input.ALIAS_MLOT2,
                MLotCodes.WaitStatus,
                MLotCodes.WaitStatus,
                LotSid: null,
                Lot: null,
                Wo: null,
                PartNo: input.PART_NO.Trim(),
                MlotQty: input.MLOT_QTY,
                TransationQty: input.MLOT_QTY,
                BohMlotQty: 0,
                ActionCode: MLotCodes.Create,
                ReasonSid: null,
                ReasonCode: null,
                InputFormName: input.INPUT_FORM_NAME,
                CreateUser: input.ACCOUNT_NO.Trim(),
                CreateTime: now,
                ReportTime: reportTime,
                PreHistSid: histSid,
                Location01: null,
                Location02: null,
                Location03: null,
                Location04: null,
                Comment: input.COMMENT),
            ct);
    }

    /// <summary>
    /// 消耗物料批。舊版 MLotConsume 會扣減庫存，必要時將狀態改 Finished，
    /// 並建立 LOT 與 MLOT 的目前使用關聯 WIP_LOT_KP_CUR_USED。
    /// </summary>
    /// <remarks>
    /// 消耗 MLOT 庫存：扣減庫存、寫入 MLOT_CONSUME 履歷，並建立 LOT 與 MLOT 的目前使用關係。
    /// </remarks>
    private async Task MLotConsumeInTxAsync(SqlConnection conn, SqlTransaction tx, MmsMLotConsumeInputDto input, CancellationToken ct)
    {
        ValidateMLotConsumeInput(input);
        await EnsureUserExistsAsync(conn, tx, input.ACCOUNT_NO, ct);
        await EnsureMlotStatusExistsAsync(conn, tx, MLotCodes.WaitStatus, ct);
        await EnsureMlotStatusExistsAsync(conn, tx, MLotCodes.FinishedStatus, ct);

        var mlot = await GetMlotAsync(conn, tx, input.MLOT, ct);
        var lot = await GetLotAsync(conn, tx, input.LOT, ct);
        var bohQty = mlot.MLOT_QTY;
        var newQty = bohQty - input.CONSUME_QTY;
        if (newQty < 0)
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, $"MLOT_QTY is insufficient: {input.MLOT}");

        var targetStatus = newQty == 0 ? MLotCodes.FinishedStatus : MLotCodes.WaitStatus;
        var now = await GetDbNowInTxAsync(conn, tx, ct);
        var reportTime = input.REPORT_TIME ?? now;
        var histSid = RandomHelper.GenerateRandomDecimal();

        await InsertMlotHistAsync(
            conn,
            tx,
            BuildMlotQuantityHist(histSid, input.DATA_LINK_SID, mlot, lot, targetStatus, -input.CONSUME_QTY, newQty, bohQty, MLotCodes.Consume, input.ACCOUNT_NO, now, reportTime, input.INPUT_FORM_NAME, input.COMMENT),
            ct);

        if (!await MlotCurUsedExistsAsync(conn, tx, lot.LOT, mlot.MLOT, ct))
            await InsertMlotCurUsedAsync(conn, tx, input.DATA_LINK_SID, lot, mlot, bohQty, input.CONSUME_QTY, newQty, now, input.COMMENT, ct);

        await UpdateMlotQuantityAndStatusAsync(conn, tx, mlot, targetStatus, newQty, histSid, input.ACCOUNT_NO, now, ct);
    }

    /// <summary>
    /// 取消消耗物料批。舊版 MLotUNConsume 會把數量加回庫存，並刪除 LOT 與 MLOT 的目前使用關聯。
    /// 依舊版邏輯，狀態維持目前狀態，不自動從 Finished 還原成 Wait。
    /// </summary>
    /// <remarks>
    /// 取消消耗 MLOT 庫存：加回庫存、寫入 MLOT_UNCONSUME 履歷，並移除目前使用關係。
    /// 舊版不會自動把 Finished 改回 Wait，因此這裡維持目前狀態。
    /// </remarks>
    private async Task MLotUNConsumeInTxAsync(SqlConnection conn, SqlTransaction tx, MmsMLotUNConsumeInputDto input, CancellationToken ct)
    {
        ValidateMLotUNConsumeInput(input);
        await EnsureUserExistsAsync(conn, tx, input.ACCOUNT_NO, ct);

        var mlot = await GetMlotAsync(conn, tx, input.MLOT, ct);
        var lot = await GetLotAsync(conn, tx, input.LOT, ct);
        await EnsureMlotStatusExistsAsync(conn, tx, mlot.MLOT_STATUS_CODE, ct);

        var bohQty = mlot.MLOT_QTY;
        var newQty = bohQty + input.UNCONSUME_QTY;
        var now = await GetDbNowInTxAsync(conn, tx, ct);
        var reportTime = input.REPORT_TIME ?? now;
        var histSid = RandomHelper.GenerateRandomDecimal();

        await InsertMlotHistAsync(
            conn,
            tx,
            BuildMlotQuantityHist(histSid, input.DATA_LINK_SID, mlot, lot, mlot.MLOT_STATUS_CODE, input.UNCONSUME_QTY, newQty, bohQty, MLotCodes.UnConsume, input.ACCOUNT_NO, now, reportTime, input.INPUT_FORM_NAME, input.COMMENT),
            ct);

        await DeleteMlotCurUsedAsync(conn, tx, lot.LOT, mlot.MLOT, ct);
        await UpdateMlotQuantityAndStatusAsync(conn, tx, mlot, mlot.MLOT_STATUS_CODE, newQty, histSid, input.ACCOUNT_NO, now, ct);
    }

    /// <summary>
    /// 變更物料批狀態。舊版 MLotStateChange 只改狀態、不異動數量，
    /// REASON_CODE 可傳可不傳，查得到 ADM_REASON 才寫入履歷 reason 欄位。
    /// </summary>
    /// <remarks>
    /// 變更 MLOT 狀態：只更新狀態與履歷，不異動庫存數量；REASON_CODE 查得到 ADM_REASON 才寫入。
    /// </remarks>
    private async Task MLotStateChangeInTxAsync(SqlConnection conn, SqlTransaction tx, MmsMLotStateChangeInputDto input, CancellationToken ct)
    {
        ValidateMLotStateChangeInput(input);
        await EnsureUserExistsAsync(conn, tx, input.ACCOUNT_NO, ct);
        await EnsureMlotStatusExistsAsync(conn, tx, input.NEW_MLOT_STATE_CODE, ct);

        var mlot = await GetMlotAsync(conn, tx, input.MLOT, ct);
        var reason = string.IsNullOrWhiteSpace(input.REASON_CODE)
            ? null
            : await GetReasonByCodeOrDefaultAsync(conn, tx, input.REASON_CODE.Trim(), ct);
        var now = await GetDbNowInTxAsync(conn, tx, ct);
        var reportTime = input.REPORT_TIME ?? now;
        var histSid = RandomHelper.GenerateRandomDecimal();

        await InsertMlotHistAsync(
            conn,
            tx,
            new MLotHistInsert(
                histSid,
                input.DATA_LINK_SID,
                mlot.MLOT_SID,
                mlot.MLOT,
                mlot.ALIAS_MLOT1,
                mlot.ALIAS_MLOT2,
                input.NEW_MLOT_STATE_CODE.Trim(),
                mlot.MLOT_STATUS_CODE,
                LotSid: null,
                Lot: null,
                Wo: null,
                PartNo: mlot.PART_NO,
                MlotQty: mlot.MLOT_QTY,
                TransationQty: 0,
                BohMlotQty: mlot.MLOT_QTY,
                ActionCode: MLotCodes.StateChange,
                ReasonSid: reason?.ADM_REASON_SID,
                ReasonCode: reason == null ? null : input.REASON_CODE?.Trim(),
                InputFormName: input.INPUT_FORM_NAME,
                CreateUser: input.ACCOUNT_NO.Trim(),
                CreateTime: now,
                ReportTime: reportTime,
                PreHistSid: mlot.LAST_MMS_MLOT_HIST_SID ?? histSid,
                Location01: mlot.MMS_LOCATION_01_NO,
                Location02: mlot.MMS_LOCATION_02_NO,
                Location03: mlot.MMS_LOCATION_03_NO,
                Location04: mlot.MMS_LOCATION_04_NO,
                Comment: input.COMMENT),
            ct);

        await UpdateMlotStatusAsync(conn, tx, mlot, input.NEW_MLOT_STATE_CODE.Trim(), histSid, input.ACCOUNT_NO, now, ct);
    }

    private static MLotHistInsert BuildMlotQuantityHist(
        decimal histSid,
        decimal dataLinkSid,
        MLotRow mlot,
        LotRow lot,
        string statusCode,
        decimal transationQty,
        decimal newQty,
        decimal bohQty,
        string actionCode,
        string accountNo,
        DateTime createTime,
        DateTime reportTime,
        string? inputFormName,
        string? comment)
    {
        return new MLotHistInsert(
            histSid,
            dataLinkSid,
            mlot.MLOT_SID,
            mlot.MLOT,
            mlot.ALIAS_MLOT1,
            mlot.ALIAS_MLOT2,
            statusCode,
            mlot.MLOT_STATUS_CODE,
            lot.LOT_SID,
            lot.LOT,
            lot.WO,
            mlot.PART_NO,
            newQty,
            transationQty,
            bohQty,
            actionCode,
            ReasonSid: null,
            ReasonCode: null,
            InputFormName: inputFormName,
            CreateUser: accountNo.Trim(),
            CreateTime: createTime,
            ReportTime: reportTime,
            PreHistSid: mlot.LAST_MMS_MLOT_HIST_SID ?? histSid,
            Location01: mlot.MMS_LOCATION_01_NO,
            Location02: mlot.MMS_LOCATION_02_NO,
            Location03: mlot.MMS_LOCATION_03_NO,
            Location04: mlot.MMS_LOCATION_04_NO,
            Comment: comment);
    }

    private static void ValidateCreateMLotInput(MmsCreateMLotInputDto input)
    {
        if (input.DATA_LINK_SID <= 0)
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, "DATA_LINK_SID must be greater than 0.");
        if (string.IsNullOrWhiteSpace(input.MLOT))
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, "MLOT is required.");
        if (string.IsNullOrWhiteSpace(input.PART_NO))
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, "PART_NO is required.");
        if (input.MLOT_QTY <= 0)
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, "MLOT_QTY must be greater than 0.");
        if (string.IsNullOrWhiteSpace(input.ACCOUNT_NO))
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, "ACCOUNT_NO is required.");
    }

    private static void ValidateMLotConsumeInput(MmsMLotConsumeInputDto input)
    {
        ValidateMlotLotInput(input.DATA_LINK_SID, input.MLOT, input.LOT, input.ACCOUNT_NO);
        if (input.CONSUME_QTY <= 0)
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, "CONSUME_QTY must be greater than 0.");
    }

    private static void ValidateMLotUNConsumeInput(MmsMLotUNConsumeInputDto input)
    {
        ValidateMlotLotInput(input.DATA_LINK_SID, input.MLOT, input.LOT, input.ACCOUNT_NO);
        if (input.UNCONSUME_QTY <= 0)
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, "UNCONSUME_QTY must be greater than 0.");
    }

    private static void ValidateMlotLotInput(decimal dataLinkSid, string mlot, string lot, string accountNo)
    {
        if (dataLinkSid <= 0)
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, "DATA_LINK_SID must be greater than 0.");
        if (string.IsNullOrWhiteSpace(mlot))
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, "MLOT is required.");
        if (string.IsNullOrWhiteSpace(lot))
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, "LOT is required.");
        if (string.IsNullOrWhiteSpace(accountNo))
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, "ACCOUNT_NO is required.");
    }

    private static void ValidateMLotStateChangeInput(MmsMLotStateChangeInputDto input)
    {
        if (input.DATA_LINK_SID <= 0)
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, "DATA_LINK_SID must be greater than 0.");
        if (string.IsNullOrWhiteSpace(input.MLOT))
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, "MLOT is required.");
        if (string.IsNullOrWhiteSpace(input.NEW_MLOT_STATE_CODE))
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, "NEW_MLOT_STATE_CODE is required.");
        if (string.IsNullOrWhiteSpace(input.ACCOUNT_NO))
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, "ACCOUNT_NO is required.");
    }

    private static async Task InsertMlotAsync(SqlConnection conn, SqlTransaction tx, MLotInsert mlot, CancellationToken ct)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
                          INSERT INTO MMS_MLOT (
                              MLOT_SID, MLOT, MLOT_TYPE, MLOT_WO, PART_NO, MLOT_QTY,
                              PARENT_MLOT, MLOT_STATUS_CODE, EXPIRY_DATE, ALIAS_MLOT1,
                              ALIAS_MLOT2, DATE_CODE, LAST_TRANS_TIME, LAST_STATUS_CHANGE_TIME,
                              LAST_MMS_MLOT_HIST_SID, COMMENT, CREATE_USER, CREATE_TIME,
                              EDIT_USER, EDIT_TIME
                          )
                          VALUES (
                              @MlotSid, @Mlot, @MlotType, @MlotWo, @PartNo, @MlotQty,
                              @ParentMlot, @MlotStatusCode, @ExpiryDate, @AliasMlot1,
                              @AliasMlot2, @DateCode, @Now, @Now,
                              @LastHistSid, @Comment, @User, @Now,
                              @User, @Now
                          );
                          """;
        cmd.Parameters.Add(new SqlParameter("@MlotSid", SqlDbType.Decimal) { Value = mlot.MlotSid });
        cmd.Parameters.Add(new SqlParameter("@Mlot", SqlDbType.NVarChar, 100) { Value = mlot.Mlot });
        cmd.Parameters.Add(new SqlParameter("@MlotType", SqlDbType.NVarChar, 100) { Value = (object?)mlot.MlotType ?? DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@MlotWo", SqlDbType.NVarChar, 100) { Value = (object?)mlot.MlotWo ?? DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@PartNo", SqlDbType.NVarChar, 100) { Value = mlot.PartNo });
        cmd.Parameters.Add(new SqlParameter("@MlotQty", SqlDbType.Decimal) { Value = mlot.MlotQty });
        cmd.Parameters.Add(new SqlParameter("@ParentMlot", SqlDbType.NVarChar, 100) { Value = (object?)mlot.ParentMlot ?? DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@MlotStatusCode", SqlDbType.NVarChar, 100) { Value = mlot.MlotStatusCode });
        cmd.Parameters.Add(new SqlParameter("@ExpiryDate", SqlDbType.DateTime) { Value = (object?)mlot.ExpiryDate ?? DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@AliasMlot1", SqlDbType.NVarChar, 100) { Value = (object?)mlot.AliasMlot1 ?? DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@AliasMlot2", SqlDbType.NVarChar, 100) { Value = (object?)mlot.AliasMlot2 ?? DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@DateCode", SqlDbType.NVarChar, 100) { Value = (object?)mlot.DateCode ?? DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@LastHistSid", SqlDbType.Decimal) { Value = mlot.LastHistSid });
        cmd.Parameters.Add(new SqlParameter("@Comment", SqlDbType.NVarChar, 255) { Value = (object?)mlot.Comment ?? DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@User", SqlDbType.NVarChar, 100) { Value = mlot.User });
        cmd.Parameters.Add(new SqlParameter("@Now", SqlDbType.DateTime) { Value = mlot.Now });
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task InsertMlotHistAsync(SqlConnection conn, SqlTransaction tx, MLotHistInsert hist, CancellationToken ct)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
                          INSERT INTO MMS_MLOT_HIST (
                              MMS_MLOT_HIST_SID, DATA_LINK_SID, MLOT_SID, MLOT,
                              ALIAS_MLOT1, ALIAS_MLOT2, MLOT_STATUS_CODE, PRE_MLOT_STATUS_CODE,
                              LOT_SID, LOT, WO, PART_NO, MLOT_QTY, TRANSATION_QTY, BOH_MLOT_QTY,
                              ACTION_CODE, REASON_SID, REASON_CODE, INPUT_FORM_NAME, CREATE_USER,
                              CREATE_TIME, REPORT_TIME, PRE_MMS_MLOT_HIST_SID,
                              MMS_LOCATION_01_NO, MMS_LOCATION_02_NO, MMS_LOCATION_03_NO,
                              MMS_LOCATION_04_NO, COMMENT
                          )
                          VALUES (
                              @HistSid, @DataLinkSid, @MlotSid, @Mlot,
                              @AliasMlot1, @AliasMlot2, @StatusCode, @PreStatusCode,
                              @LotSid, @Lot, @Wo, @PartNo, @MlotQty, @TransationQty, @BohMlotQty,
                              @ActionCode, @ReasonSid, @ReasonCode, @InputFormName, @CreateUser,
                              @CreateTime, @ReportTime, @PreHistSid,
                              @Location01, @Location02, @Location03,
                              @Location04, @Comment
                          );
                          """;
        cmd.Parameters.Add(new SqlParameter("@HistSid", SqlDbType.Decimal) { Value = hist.HistSid });
        cmd.Parameters.Add(new SqlParameter("@DataLinkSid", SqlDbType.Decimal) { Value = hist.DataLinkSid });
        cmd.Parameters.Add(new SqlParameter("@MlotSid", SqlDbType.Decimal) { Value = hist.MlotSid });
        cmd.Parameters.Add(new SqlParameter("@Mlot", SqlDbType.NVarChar, 100) { Value = hist.Mlot });
        cmd.Parameters.Add(new SqlParameter("@AliasMlot1", SqlDbType.NVarChar, 100) { Value = (object?)hist.AliasMlot1 ?? DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@AliasMlot2", SqlDbType.NVarChar, 100) { Value = (object?)hist.AliasMlot2 ?? DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@StatusCode", SqlDbType.NVarChar, 100) { Value = hist.StatusCode });
        cmd.Parameters.Add(new SqlParameter("@PreStatusCode", SqlDbType.NVarChar, 100) { Value = (object?)hist.PreStatusCode ?? DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@LotSid", SqlDbType.Decimal) { Value = (object?)hist.LotSid ?? DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@Lot", SqlDbType.NVarChar, 100) { Value = (object?)hist.Lot ?? DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@Wo", SqlDbType.NVarChar, 100) { Value = (object?)hist.Wo ?? DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@PartNo", SqlDbType.NVarChar, 100) { Value = hist.PartNo });
        cmd.Parameters.Add(new SqlParameter("@MlotQty", SqlDbType.Decimal) { Value = (object?)hist.MlotQty ?? DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@TransationQty", SqlDbType.Decimal) { Value = (object?)hist.TransationQty ?? DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@BohMlotQty", SqlDbType.Decimal) { Value = (object?)hist.BohMlotQty ?? DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@ActionCode", SqlDbType.NVarChar, 100) { Value = (object?)hist.ActionCode ?? DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@ReasonSid", SqlDbType.Decimal) { Value = (object?)hist.ReasonSid ?? DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@ReasonCode", SqlDbType.NVarChar, 100) { Value = (object?)hist.ReasonCode ?? DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@InputFormName", SqlDbType.NVarChar, 100) { Value = (object?)hist.InputFormName ?? DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@CreateUser", SqlDbType.NVarChar, 100) { Value = (object?)hist.CreateUser ?? DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@CreateTime", SqlDbType.DateTime) { Value = hist.CreateTime });
        cmd.Parameters.Add(new SqlParameter("@ReportTime", SqlDbType.DateTime) { Value = hist.ReportTime });
        cmd.Parameters.Add(new SqlParameter("@PreHistSid", SqlDbType.Decimal) { Value = (object?)hist.PreHistSid ?? DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@Location01", SqlDbType.NVarChar, 100) { Value = (object?)hist.Location01 ?? DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@Location02", SqlDbType.NVarChar, 100) { Value = (object?)hist.Location02 ?? DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@Location03", SqlDbType.NVarChar, 100) { Value = (object?)hist.Location03 ?? DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@Location04", SqlDbType.NVarChar, 100) { Value = (object?)hist.Location04 ?? DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@Comment", SqlDbType.NVarChar, 255) { Value = (object?)hist.Comment ?? DBNull.Value });
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task InsertMlotCurUsedAsync(SqlConnection conn, SqlTransaction tx, decimal dataLinkSid, LotRow lot, MLotRow mlot, decimal bohQty, decimal consumeQty, decimal newQty, DateTime now, string? comment, CancellationToken ct)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
                          INSERT INTO WIP_LOT_KP_CUR_USED (
                              WIP_LOT_KP_CUR_USED_SID, DATA_LINK_SID, WIP_LOT_SID, WIP_LOT,
                              MLOT_SID, MLOT_TYPE, MLOT, PART_NO, PARENT_MLOT,
                              MLOT_BOH_QTY, MLOT_TRANSATION_QTY, MLOT_QTY,
                              MLOT_COMMENT, CREATE_TIME, COMMENT
                          )
                          VALUES (
                              @Sid, @DataLinkSid, @LotSid, @Lot,
                              @MlotSid, @MlotType, @Mlot, @PartNo, @ParentMlot,
                              @BohQty, @ConsumeQty, @NewQty,
                              @MlotComment, @CreateTime, @Comment
                          );
                          """;
        cmd.Parameters.Add(new SqlParameter("@Sid", SqlDbType.Decimal) { Value = RandomHelper.GenerateRandomDecimal() });
        cmd.Parameters.Add(new SqlParameter("@DataLinkSid", SqlDbType.Decimal) { Value = dataLinkSid });
        cmd.Parameters.Add(new SqlParameter("@LotSid", SqlDbType.Decimal) { Value = lot.LOT_SID });
        cmd.Parameters.Add(new SqlParameter("@Lot", SqlDbType.NVarChar, 100) { Value = lot.LOT });
        cmd.Parameters.Add(new SqlParameter("@MlotSid", SqlDbType.Decimal) { Value = mlot.MLOT_SID });
        cmd.Parameters.Add(new SqlParameter("@MlotType", SqlDbType.NVarChar, 100) { Value = (object?)mlot.MLOT_TYPE ?? DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@Mlot", SqlDbType.NVarChar, 100) { Value = mlot.MLOT });
        cmd.Parameters.Add(new SqlParameter("@PartNo", SqlDbType.NVarChar, 100) { Value = (object?)mlot.PART_NO ?? DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@ParentMlot", SqlDbType.NVarChar, 100) { Value = (object?)mlot.PARENT_MLOT ?? DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@BohQty", SqlDbType.Decimal) { Value = bohQty });
        cmd.Parameters.Add(new SqlParameter("@ConsumeQty", SqlDbType.Decimal) { Value = consumeQty });
        cmd.Parameters.Add(new SqlParameter("@NewQty", SqlDbType.Decimal) { Value = newQty });
        cmd.Parameters.Add(new SqlParameter("@MlotComment", SqlDbType.NVarChar, 255) { Value = (object?)mlot.COMMENT ?? DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@CreateTime", SqlDbType.DateTime) { Value = now });
        cmd.Parameters.Add(new SqlParameter("@Comment", SqlDbType.NVarChar, 255) { Value = (object?)comment ?? DBNull.Value });
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task UpdateMlotQuantityAndStatusAsync(SqlConnection conn, SqlTransaction tx, MLotRow mlot, string statusCode, decimal newQty, decimal histSid, string editUser, DateTime editTime, CancellationToken ct)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
                          UPDATE MMS_MLOT
                          SET MLOT_STATUS_CODE = @StatusCode,
                              MLOT_QTY = @MlotQty,
                              LAST_MMS_MLOT_HIST_SID = @HistSid,
                              LAST_TRANS_TIME = @EditTime,
                              LAST_STATUS_CHANGE_TIME = @EditTime,
                              EDIT_USER = @EditUser,
                              EDIT_TIME = @EditTime
                          WHERE MLOT_SID = @MlotSid
                            AND EDIT_TIME = @OriginalEditTime;
                          """;
        cmd.Parameters.Add(new SqlParameter("@StatusCode", SqlDbType.NVarChar, 100) { Value = statusCode });
        cmd.Parameters.Add(new SqlParameter("@MlotQty", SqlDbType.Decimal) { Value = newQty });
        cmd.Parameters.Add(new SqlParameter("@HistSid", SqlDbType.Decimal) { Value = histSid });
        cmd.Parameters.Add(new SqlParameter("@EditTime", SqlDbType.DateTime) { Value = editTime });
        cmd.Parameters.Add(new SqlParameter("@EditUser", SqlDbType.NVarChar, 100) { Value = editUser.Trim() });
        cmd.Parameters.Add(new SqlParameter("@MlotSid", SqlDbType.Decimal) { Value = mlot.MLOT_SID });
        cmd.Parameters.Add(new SqlParameter("@OriginalEditTime", SqlDbType.DateTime) { Value = mlot.EDIT_TIME });
        var affectedRows = await cmd.ExecuteNonQueryAsync(ct);
        if (affectedRows != 1)
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.Conflict, $"MLOT update failed because the data was modified concurrently: {mlot.MLOT}");
    }

    private static async Task UpdateMlotStatusAsync(SqlConnection conn, SqlTransaction tx, MLotRow mlot, string statusCode, decimal histSid, string editUser, DateTime editTime, CancellationToken ct)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
                          UPDATE MMS_MLOT
                          SET MLOT_STATUS_CODE = @StatusCode,
                              LAST_MMS_MLOT_HIST_SID = @HistSid,
                              LAST_TRANS_TIME = @EditTime,
                              LAST_STATUS_CHANGE_TIME = @EditTime,
                              EDIT_USER = @EditUser,
                              EDIT_TIME = @EditTime
                          WHERE MLOT_SID = @MlotSid
                            AND EDIT_TIME = @OriginalEditTime;
                          """;
        cmd.Parameters.Add(new SqlParameter("@StatusCode", SqlDbType.NVarChar, 100) { Value = statusCode });
        cmd.Parameters.Add(new SqlParameter("@HistSid", SqlDbType.Decimal) { Value = histSid });
        cmd.Parameters.Add(new SqlParameter("@EditTime", SqlDbType.DateTime) { Value = editTime });
        cmd.Parameters.Add(new SqlParameter("@EditUser", SqlDbType.NVarChar, 100) { Value = editUser.Trim() });
        cmd.Parameters.Add(new SqlParameter("@MlotSid", SqlDbType.Decimal) { Value = mlot.MLOT_SID });
        cmd.Parameters.Add(new SqlParameter("@OriginalEditTime", SqlDbType.DateTime) { Value = mlot.EDIT_TIME });
        var affectedRows = await cmd.ExecuteNonQueryAsync(ct);
        if (affectedRows != 1)
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.Conflict, $"MLOT status update failed because the data was modified concurrently: {mlot.MLOT}");
    }

    private static async Task DeleteMlotCurUsedAsync(SqlConnection conn, SqlTransaction tx, string lot, string mlot, CancellationToken ct)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "DELETE FROM WIP_LOT_KP_CUR_USED WHERE WIP_LOT = @Lot AND MLOT = @Mlot;";
        cmd.Parameters.Add(new SqlParameter("@Lot", SqlDbType.NVarChar, 100) { Value = lot });
        cmd.Parameters.Add(new SqlParameter("@Mlot", SqlDbType.NVarChar, 100) { Value = mlot });
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task EnsureUserExistsAsync(SqlConnection conn, SqlTransaction tx, string accountNo, CancellationToken ct)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "SELECT COUNT(1) FROM ADM_OPI_USER WHERE ACCOUNT_NO = @AccountNo;";
        cmd.Parameters.Add(new SqlParameter("@AccountNo", SqlDbType.NVarChar, 100) { Value = accountNo.Trim() });
        var count = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
        if (count == 0)
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, $"User not found: {accountNo}");
    }

    private static async Task EnsureMlotNotExistsAsync(SqlConnection conn, SqlTransaction tx, string mlot, CancellationToken ct)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "SELECT COUNT(1) FROM MMS_MLOT WHERE MLOT = @Mlot;";
        cmd.Parameters.Add(new SqlParameter("@Mlot", SqlDbType.NVarChar, 100) { Value = mlot.Trim() });
        var count = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
        if (count > 0)
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.Conflict, $"MLOT already exists: {mlot}");
    }

    private static async Task EnsureMlotStatusExistsAsync(SqlConnection conn, SqlTransaction tx, string statusCode, CancellationToken ct)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "SELECT COUNT(1) FROM MMS_MLOT_STATUS WHERE MLOT_STATUS_CODE = @StatusCode;";
        cmd.Parameters.Add(new SqlParameter("@StatusCode", SqlDbType.NVarChar, 100) { Value = statusCode.Trim() });
        var count = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
        if (count == 0)
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, $"MLOT status not found: {statusCode}");
    }

    private static async Task<MLotRow> GetMlotAsync(SqlConnection conn, SqlTransaction tx, string mlot, CancellationToken ct)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
                          SELECT TOP (1)
                              MLOT_SID, MLOT, MLOT_TYPE, MLOT_WO, PART_NO, MLOT_QTY,
                              PARENT_MLOT, MLOT_STATUS_CODE, ALIAS_MLOT1, ALIAS_MLOT2,
                              LAST_MMS_MLOT_HIST_SID, MMS_LOCATION_01_NO, MMS_LOCATION_02_NO,
                              MMS_LOCATION_03_NO, MMS_LOCATION_04_NO, COMMENT, EDIT_TIME
                          FROM MMS_MLOT
                          WHERE MLOT = @Mlot;
                          """;
        cmd.Parameters.Add(new SqlParameter("@Mlot", SqlDbType.NVarChar, 100) { Value = mlot.Trim() });
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, $"MLOT not found: {mlot}");

        return new MLotRow(
            reader.GetDecimal(reader.GetOrdinal("MLOT_SID")),
            reader.GetString(reader.GetOrdinal("MLOT")),
            GetNullableString(reader, "MLOT_TYPE"),
            GetNullableString(reader, "MLOT_WO"),
            reader.GetString(reader.GetOrdinal("PART_NO")),
            reader.GetDecimal(reader.GetOrdinal("MLOT_QTY")),
            GetNullableString(reader, "PARENT_MLOT"),
            reader.GetString(reader.GetOrdinal("MLOT_STATUS_CODE")),
            GetNullableString(reader, "ALIAS_MLOT1"),
            GetNullableString(reader, "ALIAS_MLOT2"),
            GetNullableDecimal(reader, "LAST_MMS_MLOT_HIST_SID"),
            GetNullableString(reader, "MMS_LOCATION_01_NO"),
            GetNullableString(reader, "MMS_LOCATION_02_NO"),
            GetNullableString(reader, "MMS_LOCATION_03_NO"),
            GetNullableString(reader, "MMS_LOCATION_04_NO"),
            GetNullableString(reader, "COMMENT"),
            reader.GetDateTime(reader.GetOrdinal("EDIT_TIME")));
    }

    private static async Task<LotRow> GetLotAsync(SqlConnection conn, SqlTransaction tx, string lot, CancellationToken ct)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "SELECT TOP (1) LOT_SID, LOT, WO FROM WIP_LOT WHERE LOT = @Lot;";
        cmd.Parameters.Add(new SqlParameter("@Lot", SqlDbType.NVarChar, 100) { Value = lot.Trim() });
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, $"LOT not found: {lot}");

        return new LotRow(
            reader.GetDecimal(reader.GetOrdinal("LOT_SID")),
            reader.GetString(reader.GetOrdinal("LOT")),
            reader.GetString(reader.GetOrdinal("WO")));
    }

    private static async Task<ReasonRow?> GetReasonByCodeOrDefaultAsync(SqlConnection conn, SqlTransaction tx, string reasonCode, CancellationToken ct)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "SELECT TOP (1) ADM_REASON_SID, REASON_NO FROM ADM_REASON WHERE REASON_NO = @ReasonNo;";
        cmd.Parameters.Add(new SqlParameter("@ReasonNo", SqlDbType.NVarChar, 255) { Value = reasonCode });
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return null;

        return new ReasonRow(
            reader.GetDecimal(reader.GetOrdinal("ADM_REASON_SID")),
            GetNullableString(reader, "REASON_NO"));
    }

    private static async Task<bool> MlotCurUsedExistsAsync(SqlConnection conn, SqlTransaction tx, string lot, string mlot, CancellationToken ct)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "SELECT COUNT(1) FROM WIP_LOT_KP_CUR_USED WHERE WIP_LOT = @Lot AND MLOT = @Mlot;";
        cmd.Parameters.Add(new SqlParameter("@Lot", SqlDbType.NVarChar, 100) { Value = lot });
        cmd.Parameters.Add(new SqlParameter("@Mlot", SqlDbType.NVarChar, 100) { Value = mlot });
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct)) > 0;
    }

    private static async Task<DateTime> GetDbNowInTxAsync(SqlConnection conn, SqlTransaction tx, CancellationToken ct)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "SELECT SYSDATETIME();";
        var value = await cmd.ExecuteScalarAsync(ct);
        return value is DateTime dt ? dt : DateTime.Now;
    }

    private static string? GetNullableString(SqlDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private static decimal? GetNullableDecimal(SqlDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetDecimal(ordinal);
    }

    private sealed record MLotInsert(
        decimal MlotSid,
        string Mlot,
        string? MlotType,
        string? MlotWo,
        string PartNo,
        decimal MlotQty,
        string? ParentMlot,
        string MlotStatusCode,
        DateTime? ExpiryDate,
        string? AliasMlot1,
        string? AliasMlot2,
        string? DateCode,
        decimal LastHistSid,
        string? Comment,
        string User,
        DateTime Now);

    private sealed record MLotHistInsert(
        decimal HistSid,
        decimal DataLinkSid,
        decimal MlotSid,
        string Mlot,
        string? AliasMlot1,
        string? AliasMlot2,
        string StatusCode,
        string? PreStatusCode,
        decimal? LotSid,
        string? Lot,
        string? Wo,
        string PartNo,
        decimal? MlotQty,
        decimal? TransationQty,
        decimal? BohMlotQty,
        string? ActionCode,
        decimal? ReasonSid,
        string? ReasonCode,
        string? InputFormName,
        string? CreateUser,
        DateTime CreateTime,
        DateTime ReportTime,
        decimal? PreHistSid,
        string? Location01,
        string? Location02,
        string? Location03,
        string? Location04,
        string? Comment);

    private sealed record MLotRow(
        decimal MLOT_SID,
        string MLOT,
        string? MLOT_TYPE,
        string? MLOT_WO,
        string PART_NO,
        decimal MLOT_QTY,
        string? PARENT_MLOT,
        string MLOT_STATUS_CODE,
        string? ALIAS_MLOT1,
        string? ALIAS_MLOT2,
        decimal? LAST_MMS_MLOT_HIST_SID,
        string? MMS_LOCATION_01_NO,
        string? MMS_LOCATION_02_NO,
        string? MMS_LOCATION_03_NO,
        string? MMS_LOCATION_04_NO,
        string? COMMENT,
        DateTime EDIT_TIME);

    private sealed record LotRow(decimal LOT_SID, string LOT, string WO);

    private sealed record ReasonRow(decimal ADM_REASON_SID, string? REASON_NO);
}

using System.Data;
using DbExtensions;
using DcMateClassLibrary.Helper;
using DcMateClassLibrary.Helper.HttpHelper;
using DcMateH5.Abstractions.CurrentUser;
using DcMateH5.Abstractions.Eqm;
using DcMateH5.Abstractions.Eqm.Models;
using DcMateH5Api.Models;
using Microsoft.Data.SqlClient;

namespace DcMateH5.Infrastructure.Eqm;

/// <summary>
/// 設備狀態切換服務
/// 
/// 主要負責：
/// 1. 驗證前端傳入的設備、狀態、原因是否存在且可用
/// 2. 查詢目前設備最新一筆狀態歷程，作為本次 FROM 狀態來源
/// 3. 寫入 EQM_STATUS_CHANGE_HIST 狀態切換歷程
/// 4. 依照 UPDATE_EQM_MASTER 決定是否同步更新 EQM_MASTER 目前狀態
/// 
/// 注意：
/// - 狀態歷程 INSERT 與主檔 UPDATE 會包在同一個 DB Transaction
/// - 任一 SQL 失敗會 rollback，避免歷程有寫入但主檔沒更新的不一致狀況
/// </summary>
public class EqmStatusService : IEqmStatusService
{
    private readonly SQLGenerateHelper _sqlHelper;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public EqmStatusService(SQLGenerateHelper sqlHelper, ICurrentUserAccessor currentUserAccessor)
    {
        _sqlHelper = sqlHelper;
        _currentUserAccessor = currentUserAccessor;
    }
    
    public static class SystemUsers
    {
        public const string CgiUser = "CGI_USER";
    }

    /// <summary>
    /// 對外提供設備狀態切換 API
    /// 
    /// 這裡只負責開啟交易與回傳結果；
    /// 實際業務流程會交給 StatusChangeInTxAsync 處理
    /// </summary>
    public async Task<Result<bool>> StatusChangeAsync(EqmStatusChangeInputDto input, CancellationToken ct = default)
    {
        await _sqlHelper.TxAsync(
            async (conn, tx, innerCt) =>
            {
                // 所有狀態切換相關 SQL 都會共用同一個 connection + transaction
                await StatusChangeInTxAsync(conn, tx, input, innerCt);
            },
            // ReadCommitted 避免讀到未提交資料，是一般狀態切換流程的合理預設
            isolation: IsolationLevel.ReadCommitted,
            ct: ct);

        return Result<bool>.Ok(true);
    }

    /// <summary>
    /// 在單一交易內完成設備狀態切換。
    /// 
    /// 流程：
    /// 1. 驗證 input 必填欄位
    /// 2. 取得目前操作人員帳號
    /// 3. 查詢設備主檔 EQM_MASTER
    /// 4. 查詢目標狀態 EQM_STATUS
    /// 5. 查詢觸發原因 EQM_REASON
    /// 6. 取得資料庫目前時間，避免使用 AP Server 時間造成時間不一致
    /// 7. 依 REPORT_TIME 解析班別資訊
    /// 8. 查詢設備最新一筆狀態歷程，作為 FROM 狀態
    /// 9. 寫入狀態切換歷程
    /// 10. 若 UPDATE_EQM_MASTER = true，更新設備主檔目前狀態
    /// </summary>
    private async Task StatusChangeInTxAsync(
        SqlConnection conn,
        SqlTransaction tx,
        EqmStatusChangeInputDto input,
        CancellationToken ct)
    {
        // 若外部請求已取消，這裡立即中斷，避免繼續執行 DB 操作
        ct.ThrowIfCancellationRequested();
        
        // 驗證前端傳入的必要欄位，避免後續 SQL 查詢或 INSERT 時發生資料不完整
        ValidateInput(input);

        // 取得目前登入使用者，作為 TRIG_USER / CREATE_USER / EDIT_USER
        var operatorAccount = _currentUserAccessor.Get()?.Account;

        if (string.IsNullOrWhiteSpace(operatorAccount))
        {
            operatorAccount = SystemUsers.CgiUser;
        }

        // 查設備主檔，確認設備存在
        var eqm = await GetEqmMasterInTxAsync(conn, tx, input.EQM_NO, ct);
        
        // 查目標狀態，確認狀態存在且 ENABLE_FLAG = Y
        var targetStatus = await GetEqmStatusInTxAsync(conn, tx, input.EQM_STATUS_NO, ct);
        
        // 查原因代碼，確認原因存在且 ENABLE_FLAG = Y
        var reason = await GetReasonInTxAsync(conn, tx, input.REASON_NO, ct);
        
        // 使用 DB Server 現在時間，讓 CREATE_TIME / EDIT_TIME 與資料庫一致
        var dbNow = await GetDbNowInTxAsync(conn, tx, ct);
        
        // 根據 REPORT_TIME 解析當下班別與 REPORT_DAY
        var shift = await ResolveShiftInfoInTxAsync(conn, tx, input.REPORT_TIME, ct);
        
        // 查詢目前設備最新一筆狀態歷程
        // 最新歷程的 TO 狀態，就是這次切換的 FROM 狀態
        var latestHist = await GetLatestStatusHistoryInTxAsync(conn, tx, eqm.EQM_MASTER_SID, ct);

        /*
         * FROM 狀態決定邏輯：
         *
         * 情境 A：有歷史紀錄
         * - FROM 狀態 = 上一筆歷程的 TO 狀態
         * - FROM 時間 = 上一筆歷程的 REPORT_TIME
         *
         * 情境 B：沒有歷史紀錄
         * - FROM 狀態 = 這次目標狀態
         * - FROM 時間 = DB 現在時間
         * - INTERVAL_SECONDS = 0
         *
         * 這樣做可以避免第一筆資料沒有 FROM 狀態而寫入失敗。
         */
        var fromStatusSid = latestHist?.TO_EQM_STATUS_SID ?? targetStatus.EQM_STATUS_SID;
        var fromStatusCode = latestHist?.TO_EQM_STATUS_CODE ?? targetStatus.EQM_STATUS_NO;
        var fromStatusName = latestHist?.TO_EQM_STATUS_NAME ?? targetStatus.EQM_STATUS_NAME;
        var fromStatusOeeType = latestHist?.TO_EQM_STATUS_OEE_TYPE ?? (targetStatus.OEE_TYPE ?? string.Empty);
        var fromStatusTime = latestHist?.REPORT_TIME ?? dbNow;
        
        /*
         * 計算本次狀態區間秒數。
         *
         * 若有上一筆歷程：
         * INTERVAL_SECONDS = 本次 REPORT_TIME - 上一筆 REPORT_TIME
         *
         * 使用 Math.Max(0, ...) 是防呆：
         * 若前端補登較早時間，避免秒數變成負數。
         */
        var intervalSeconds = latestHist == null
            ? 0
            : Math.Max(0, Convert.ToDecimal((input.REPORT_TIME - latestHist.REPORT_TIME).TotalSeconds));

        await InsertStatusHistoryInTxAsync(
            conn,
            tx,
            input,
            eqm,
            targetStatus,
            reason,
            operatorAccount,
            dbNow,
            shift,
            fromStatusSid,
            fromStatusCode,
            fromStatusName,
            fromStatusOeeType,
            fromStatusTime,
            intervalSeconds,
            ct);

        /*
         * 是否更新 EQM_MASTER 由 input.UPDATE_EQM_MASTER 控制。
         *
         * true：
         * - 寫歷程
         * - 同步更新 EQM_MASTER 目前狀態
         *
         * false：
         * - 只寫歷程
         * - 不動 EQM_MASTER
         *
         * 這種設計通常用在：
         * - 補資料
         * - 匯入歷史狀態
         * - 只想留紀錄但不影響目前設備狀態
         */
        if (input.UPDATE_EQM_MASTER)
        {
            await UpdateEqmMasterStatusInTxAsync(conn, tx, eqm, targetStatus, operatorAccount, input.REPORT_TIME, dbNow, ct);
        }
    }

    private static void ValidateInput(EqmStatusChangeInputDto input)
    {
        if (input.DATA_LINK_SID <= 0)
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, "DATA_LINK_SID must be greater than 0.");

        if (string.IsNullOrWhiteSpace(input.EQM_NO))
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, "EQM_NO is required.");

        if (string.IsNullOrWhiteSpace(input.EQM_STATUS_NO))
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, "EQM_STATUS_NO is required.");

        if (string.IsNullOrWhiteSpace(input.REASON_NO))
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, "REASON_NO is required.");

        if (input.REPORT_TIME == default)
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, "REPORT_TIME is required.");

        if (string.IsNullOrWhiteSpace(input.INPUT_FORM_NAME))
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, "INPUT_FORM_NAME is required.");
    }

    private async Task<EqmMasterDto> GetEqmMasterInTxAsync(SqlConnection conn, SqlTransaction tx, string eqmNo, CancellationToken ct)
    {
        var where = new WhereBuilder<EqmMasterDto>()
            .AndEq(x => x.EQM_MASTER_NO, eqmNo);

        var eqm = await _sqlHelper.SelectFirstOrDefaultInTxAsync(conn, tx, where, ct: ct);
        if (eqm == null)
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, $"Eqm not found: {eqmNo}");

        return eqm;
    }

    private async Task<EqmStatusDto> GetEqmStatusInTxAsync(SqlConnection conn, SqlTransaction tx, string statusNo, CancellationToken ct)
    {
        var where = new WhereBuilder<EqmStatusDto>()
            .AndEq(x => x.EQM_STATUS_NO, statusNo)
            .AndEq(x => x.ENABLE_FLAG, "Y");

        var status = await _sqlHelper.SelectFirstOrDefaultInTxAsync(conn, tx, where, ct: ct);
        if (status == null)
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, $"Eqm status not found or disabled: {statusNo}");

        return status;
    }

    private async Task<EqmReasonDto> GetReasonInTxAsync(SqlConnection conn, SqlTransaction tx, string reasonNo, CancellationToken ct)
    {
        var where = new WhereBuilder<EqmReasonDto>()
            .AndEq(x => x.REASON_NO, reasonNo)
            .AndEq(x => x.ENABLE_FLAG, "Y");

        var reason = await _sqlHelper.SelectFirstOrDefaultInTxAsync(conn, tx, where, ct: ct);
        if (reason == null)
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, $"Reason not found or disabled: {reasonNo}");

        return reason;
    }

    private async Task<EqmStatusChangeHistRow?> GetLatestStatusHistoryInTxAsync(
        SqlConnection conn,
        SqlTransaction tx,
        decimal eqmSid,
        CancellationToken ct)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
                          SELECT TOP (1)
                              [TO_EQM_STATUS_SID],
                              [TO_EQM_STATUS_CODE],
                              [TO_EQM_STATUS_NAME],
                              [TO_EQM_STATUS_OEE_TYPE],
                              [REPORT_TIME]
                          FROM [EQM_STATUS_CHANGE_HIST]
                          WHERE [EQM_SID] = @EqmSid
                          ORDER BY [CREATE_TIME] DESC, [EQM_HIST_SID] DESC;
                          """;
        cmd.Parameters.Add(new SqlParameter("@EqmSid", SqlDbType.Decimal) { Value = eqmSid });

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return null;

        return new EqmStatusChangeHistRow
        {
            TO_EQM_STATUS_SID = reader.GetDecimal(reader.GetOrdinal("TO_EQM_STATUS_SID")),
            TO_EQM_STATUS_CODE = reader.GetString(reader.GetOrdinal("TO_EQM_STATUS_CODE")),
            TO_EQM_STATUS_NAME = reader.GetString(reader.GetOrdinal("TO_EQM_STATUS_NAME")),
            TO_EQM_STATUS_OEE_TYPE = reader.GetString(reader.GetOrdinal("TO_EQM_STATUS_OEE_TYPE")),
            REPORT_TIME = reader.GetDateTime(reader.GetOrdinal("REPORT_TIME"))
        };
    }

    private async Task<EqmShiftInfoRow> ResolveShiftInfoInTxAsync(
        SqlConnection conn,
        SqlTransaction tx,
        DateTime reportTime,
        CancellationToken ct)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
                          SELECT TOP (1)
                              s.[SHIFT_NO],
                              t.[REPORT_DAY]
                          FROM [ADM_SHIFT_TIMETABLE] t
                          INNER JOIN [ADM_SHIFT] s ON CONVERT(nvarchar(255), s.[ADM_SHIFT_SID]) = t.[SHIFT_SID]
                          WHERE t.[START_TIME] < @ShiftTime
                            AND t.[END_TIME] >= @ShiftTime
                            AND t.[ENABLE_FLAG] = 'Y'
                            AND s.[ENABLE_FLAG] = 'Y'
                          ORDER BY t.[START_TIME];
                          """;
        cmd.Parameters.Add(new SqlParameter("@ShiftTime", SqlDbType.NVarChar, 8) { Value = reportTime.ToString("HH:mm:ss") });

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            return new EqmShiftInfoRow
            {
                SHIFT_NO = string.Empty,
                REPORT_DAY = "0"
            };
        }

        return new EqmShiftInfoRow
        {
            SHIFT_NO = reader.GetString(reader.GetOrdinal("SHIFT_NO")),
            REPORT_DAY = reader.GetString(reader.GetOrdinal("REPORT_DAY"))
        };
    }

    private static async Task InsertStatusHistoryInTxAsync(
        SqlConnection conn,
        SqlTransaction tx,
        EqmStatusChangeInputDto input,
        EqmMasterDto eqm,
        EqmStatusDto targetStatus,
        EqmReasonDto reason,
        string operatorAccount,
        DateTime dbNow,
        EqmShiftInfoRow shift,
        decimal fromStatusSid,
        string fromStatusCode,
        string fromStatusName,
        string fromStatusOeeType,
        DateTime fromStatusTime,
        decimal intervalSeconds,
        CancellationToken ct)
    {
        var shiftDay = input.REPORT_TIME.Date;
        if (string.Equals(shift.REPORT_DAY?.Trim(), "-1", StringComparison.OrdinalIgnoreCase))
            shiftDay = shiftDay.AddDays(-1);

        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
                          INSERT INTO [EQM_STATUS_CHANGE_HIST] (
                              [EQM_HIST_SID],
                              [DATA_LINK_SID],
                              [EQM_SID],
                              [EQM_NO],
                              [EQM_NAME],
                              [FROM_EQM_STATUS_SID],
                              [FROM_EQM_STATUS_CODE],
                              [FROM_EQM_STATUS_NAME],
                              [FROM_EQM_STATUS_OEE_TYPE],
                              [FROM_EQM_STATUS_TIME],
                              [TO_EQM_STATUS_SID],
                              [TO_EQM_STATUS_CODE],
                              [TO_EQM_STATUS_NAME],
                              [TO_EQM_STATUS_OEE_TYPE],
                              [TO_EQM_STATUS_TIME],
                              [REPORT_TIME],
                              [INTERVAL_SECONDS],
                              [TRIG_USER],
                              [TRIG_REASON_SID],
                              [TRIG_REASON_CODE],
                              [TRIG_REASON_NAME],
                              [INPUT_FORM_NAME],
                              [CREATE_USER],
                              [CREATE_TIME],
                              [SHIFT_NO],
                              [SHIFT_DAY]
                          )
                          VALUES (
                              @HistSid,
                              @DataLinkSid,
                              @EqmSid,
                              @EqmNo,
                              @EqmName,
                              @FromStatusSid,
                              @FromStatusCode,
                              @FromStatusName,
                              @FromStatusOeeType,
                              @FromStatusTime,
                              @ToStatusSid,
                              @ToStatusCode,
                              @ToStatusName,
                              @ToStatusOeeType,
                              @ToStatusTime,
                              @ReportTime,
                              @IntervalSeconds,
                              @TrigUser,
                              @ReasonSid,
                              @ReasonCode,
                              @ReasonName,
                              @InputFormName,
                              @CreateUser,
                              @CreateTime,
                              @ShiftNo,
                              @ShiftDay
                          );
                          """;

        cmd.Parameters.Add(new SqlParameter("@HistSid", SqlDbType.Decimal) { Value = RandomHelper.GenerateRandomDecimal() });
        cmd.Parameters.Add(new SqlParameter("@DataLinkSid", SqlDbType.Decimal) { Value = input.DATA_LINK_SID });
        cmd.Parameters.Add(new SqlParameter("@EqmSid", SqlDbType.Decimal) { Value = eqm.EQM_MASTER_SID });
        cmd.Parameters.Add(new SqlParameter("@EqmNo", SqlDbType.NVarChar, 255) { Value = eqm.EQM_MASTER_NO });
        cmd.Parameters.Add(new SqlParameter("@EqmName", SqlDbType.NVarChar, 255) { Value = eqm.EQM_MASTER_NAME });
        cmd.Parameters.Add(new SqlParameter("@FromStatusSid", SqlDbType.Decimal) { Value = fromStatusSid });
        cmd.Parameters.Add(new SqlParameter("@FromStatusCode", SqlDbType.NVarChar, 255) { Value = fromStatusCode });
        cmd.Parameters.Add(new SqlParameter("@FromStatusName", SqlDbType.NVarChar, 255) { Value = fromStatusName });
        cmd.Parameters.Add(new SqlParameter("@FromStatusOeeType", SqlDbType.NVarChar, 255) { Value = fromStatusOeeType });
        cmd.Parameters.Add(new SqlParameter("@FromStatusTime", SqlDbType.DateTime) { Value = fromStatusTime });
        cmd.Parameters.Add(new SqlParameter("@ToStatusSid", SqlDbType.Decimal) { Value = targetStatus.EQM_STATUS_SID });
        cmd.Parameters.Add(new SqlParameter("@ToStatusCode", SqlDbType.NVarChar, 255) { Value = targetStatus.EQM_STATUS_NO });
        cmd.Parameters.Add(new SqlParameter("@ToStatusName", SqlDbType.NVarChar, 255) { Value = targetStatus.EQM_STATUS_NAME });
        cmd.Parameters.Add(new SqlParameter("@ToStatusOeeType", SqlDbType.NVarChar, 255) { Value = targetStatus.OEE_TYPE ?? string.Empty });
        cmd.Parameters.Add(new SqlParameter("@ToStatusTime", SqlDbType.DateTime) { Value = input.REPORT_TIME });
        cmd.Parameters.Add(new SqlParameter("@ReportTime", SqlDbType.DateTime) { Value = input.REPORT_TIME });
        cmd.Parameters.Add(new SqlParameter("@IntervalSeconds", SqlDbType.Decimal) { Value = intervalSeconds });
        cmd.Parameters.Add(new SqlParameter("@TrigUser", SqlDbType.NVarChar, 255) { Value = operatorAccount });
        cmd.Parameters.Add(new SqlParameter("@ReasonSid", SqlDbType.Decimal) { Value = reason.ADM_REASON_SID });
        cmd.Parameters.Add(new SqlParameter("@ReasonCode", SqlDbType.NVarChar, 255) { Value = reason.REASON_NO ?? string.Empty });
        cmd.Parameters.Add(new SqlParameter("@ReasonName", SqlDbType.NVarChar, 255) { Value = reason.REASON_NAME ?? string.Empty });
        cmd.Parameters.Add(new SqlParameter("@InputFormName", SqlDbType.NVarChar, 255) { Value = input.INPUT_FORM_NAME });
        cmd.Parameters.Add(new SqlParameter("@CreateUser", SqlDbType.NVarChar, 255) { Value = operatorAccount });
        cmd.Parameters.Add(new SqlParameter("@CreateTime", SqlDbType.DateTime) { Value = dbNow });
        cmd.Parameters.Add(new SqlParameter("@ShiftNo", SqlDbType.NVarChar, 255) { Value = shift.SHIFT_NO ?? string.Empty });
        cmd.Parameters.Add(new SqlParameter("@ShiftDay", SqlDbType.NVarChar, 255) { Value = shiftDay.ToString("yyyy-MM-dd") });

        var affectedRows = await cmd.ExecuteNonQueryAsync(ct);
        if (affectedRows != 1)
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.Conflict, $"Eqm status history insert failed: {input.EQM_NO}");
    }

    private static async Task UpdateEqmMasterStatusInTxAsync(
        SqlConnection conn,
        SqlTransaction tx,
        EqmMasterDto eqm,
        EqmStatusDto targetStatus,
        string editUser,
        DateTime reportTime,
        DateTime editTime,
        CancellationToken ct)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
                          UPDATE [EQM_MASTER]
                          SET [STATUS] = @Status,
                              [STATUS_SID] = @StatusSid,
                              [EDIT_STATUS_TIME] = @ReportTime,
                              [STATUS_CHANGE_TIME] = @ReportTime,
                              [EDIT_USER] = @EditUser,
                              [EDIT_TIME] = @EditTime
                          WHERE [EQM_MASTER_SID] = @EqmSid;
                          """;
        cmd.Parameters.Add(new SqlParameter("@Status", SqlDbType.NVarChar, 255) { Value = targetStatus.EQM_STATUS_NO });
        cmd.Parameters.Add(new SqlParameter("@StatusSid", SqlDbType.Decimal) { Value = targetStatus.EQM_STATUS_SID });
        cmd.Parameters.Add(new SqlParameter("@ReportTime", SqlDbType.DateTime) { Value = reportTime });
        cmd.Parameters.Add(new SqlParameter("@EditUser", SqlDbType.NVarChar, 255) { Value = editUser });
        cmd.Parameters.Add(new SqlParameter("@EditTime", SqlDbType.DateTime) { Value = editTime });
        cmd.Parameters.Add(new SqlParameter("@EqmSid", SqlDbType.Decimal) { Value = eqm.EQM_MASTER_SID });

        var affectedRows = await cmd.ExecuteNonQueryAsync(ct);
        if (affectedRows != 1)
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.Conflict, $"Eqm master update failed: {eqm.EQM_MASTER_NO}");
    }

    private static async Task<DateTime> GetDbNowInTxAsync(SqlConnection conn, SqlTransaction tx, CancellationToken ct)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "SELECT SYSDATETIME();";
        return (DateTime)(await cmd.ExecuteScalarAsync(ct) ?? DateTime.Now);
    }
}

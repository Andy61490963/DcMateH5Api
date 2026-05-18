using DbExtensions;
using DcMateClassLibrary.Helper;
using DcMateClassLibrary.Helper.HttpHelper;
using DcMateH5.Abstractions.CurrentUser;
using DcMateH5.Abstractions.Eqm;
using DcMateH5.Abstractions.Eqm.Models;
using DcMateH5.Abstractions.EQM;
using DcMateH5.Abstractions.EQM.Models;
using DcMateH5Api.Models;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DcMateH5.Infrastructure.EQM;

public class EqmAutoDcService : IEqmAutoDcService
{
    private readonly SQLGenerateHelper _sqlHelper;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IConfiguration _configuration;
    // 注入同事寫好的狀態切換服務
    private readonly IEqmStatusService _eqmStatusService;

    public EqmAutoDcService(
        SQLGenerateHelper sqlHelper,
        ICurrentUserAccessor currentUserAccessor,
        IConfiguration configuration,
        IEqmStatusService eqmStatusService)
    {
        _sqlHelper = sqlHelper;
        _currentUserAccessor = currentUserAccessor;
        _configuration = configuration;
        _eqmStatusService = eqmStatusService;
    }

    public static class SystemUsers
    {
        public const string GuiUser = "GUI_USER";
    }

    public async Task<Result<bool>> ProcessAutoDcUploadAsync(EqmAutoDcInputDto input, CancellationToken ct = default)
    {
        // 用來累加本次上傳所有項目的絕對值落差，判斷機台是否有在動
        decimal totalDiffValue = 0;
        string currentDbStatus = string.Empty;

        // 1. 先在外層執行 AutoDC 的雙表資料查詢與寫入交易
        await _sqlHelper.TxAsync(
            async (conn, tx, innerCt) =>
            {
                // 核心資料處理，並回傳本次這批數據的總差異值
                totalDiffValue = await ProcessAutoDcInTxAsync(conn, tx, input, innerCt);

                // 順便查出目前主檔的真實狀態，用來滿足 SAME_CHANGE 邏輯
                currentDbStatus = await GetCurrentEqmStatusInTxAsync(conn, tx, input.EqmMasterNo, innerCt);
            },
            isolation: IsolationLevel.ReadCommitted,
            ct: ct);

        // 2. 當上述交易成功 Commit 提交後，緊接著執行原本的 AutoIdle 機況自動轉換邏輯
        if (input.AutoIdle.Equals("TRUE", StringComparison.OrdinalIgnoreCase))
        {
            // 如果總體差異值是 0 (機台沒產出)，判定為 Idle；有產出則判定為 Run
            string newEqpState = totalDiffValue == 0 ? "Idle" : "Run";

            bool isSameStatus = string.Equals(currentDbStatus?.Trim(), newEqpState, StringComparison.OrdinalIgnoreCase);
            bool shouldChange = input.SameChange.Equals("TRUE", StringComparison.OrdinalIgnoreCase) || !isSameStatus;

            if (shouldChange)
            {
                // 產生 yyyyMMddHHmmssfff 格式的 17 位數絕對不重號時間流水號
                string sidStr = DateTime.Now.ToString("yyyyMMddHHmmssfff");
                decimal generatedSid = decimal.Parse(sidStr);
                // 組裝同事約定好的狀態變更實體 DTO
                var statusInput = new EqmStatusChangeInputDto
                {
                    DATA_LINK_SID = generatedSid, // 依系統現狀給予預設或流水號
                    EQM_NO = input.EqmMasterNo,
                    EQM_STATUS_NO = newEqpState, // "Idle" 或 "Run"
                    REASON_NO = "99",      // 請確保 ADM_REASON 表中有一筆啟用的 AUTO_CGI 代碼，否則同事的 API 會報錯
                    REPORT_TIME = DateTime.Now,
                    INPUT_FORM_NAME = "CGI",
                    UPDATE_EQM_MASTER = true     // 即時狀態變更，連動更新主檔
                };

                // 呼叫同事寫好的狀態切換服務
                await _eqmStatusService.StatusChangeAsync(statusInput, ct);
            }
        }

        return Result<bool>.Ok(true);
    }

    /// <summary>
    /// 在交易內處理核心 AutoDC 數據，並回傳本次所有項目的絕對差異值總和
    /// </summary>
    private async Task<decimal> ProcessAutoDcInTxAsync(
        SqlConnection conn,
        SqlTransaction tx,
        EqmAutoDcInputDto input,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (string.IsNullOrEmpty(input.Value))
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, "VALUE is required.");

        var arrayData = input.Value.Split(',');
        var operatorAccount = _currentUserAccessor.Get()?.Account ?? SystemUsers.GuiUser;
        var dbNow = await GetDbNowInTxAsync(conn, tx, ct);

        var shift = await ResolveShiftInfoInTxAsync(conn, tx, dbNow, ct);
        var shiftDayStr = dbNow.ToString("yyyy-MM-dd");
        if (string.Equals(shift.REPORT_DAY?.Trim(), "-1", StringComparison.OrdinalIgnoreCase))
        {
            shiftDayStr = dbNow.AddDays(-1).ToString("yyyy-MM-dd");
        }

        var items = arrayData.Select(x => x.Split(':')[0]).ToList();
        string itemInClause = string.Join(",", items.Select(x => $"'{x}'"));

        string selectLastSql = $@"SELECT [AUTODC_ITEM], [AUTODC_OUTPUT] 
                                 FROM [dbo].[EQM_MASTER_AUTODC_OUTPUT_CUR] 
                                 WHERE [EQM_MASTER_NO] = @EqmMasterNo 
                                   AND [AUTODC_ITEM] IN ({itemInClause})";

        DataTable thisItemLastDt = new DataTable();
        using (var selectCmd = conn.CreateCommand())
        {
            selectCmd.Transaction = tx;
            selectCmd.CommandText = selectLastSql;
            selectCmd.Parameters.Add(new SqlParameter("@EqmMasterNo", SqlDbType.NVarChar, 255) { Value = input.EqmMasterNo });
            using var reader = await selectCmd.ExecuteReaderAsync(ct);
            thisItemLastDt.Load(reader);
        }

        decimal batchTotalDiff = 0;

        foreach (var data in arrayData)
        {
            string[] itemArray = data.Split(':');
            if (itemArray.Length < 2) continue;

            string curItem = itemArray[0].Trim();
            decimal curValue = decimal.TryParse(itemArray[1].Trim(), out var v) ? v : 0;

            decimal diffValue = 0;
            bool hasLastRecord = false;
            decimal lastValue = 0;

            if (thisItemLastDt != null && thisItemLastDt.Rows.Count > 0)
            {
                var rows = thisItemLastDt.Select($"AUTODC_ITEM='{curItem}'");
                if (rows.Length > 0)
                {
                    hasLastRecord = true;
                    lastValue = decimal.TryParse(rows[0]["AUTODC_OUTPUT"].ToString(), out var lv) ? lv : 0;
                }
            }

            decimal dbSid = RandomHelper.GenerateRandomDecimal();

            if (hasLastRecord)
            {
                // ==========================================
                // [EDC 模式] 保持原樣：無條件 這次 - 上次
                // ==========================================
                if (input.Mode.Equals("EDC", StringComparison.OrdinalIgnoreCase))
                {
                    diffValue = curValue - lastValue;

                    await InsertHistoryAsync(conn, tx, dbSid, input.EqmMasterNo, curItem, curValue, diffValue, dbNow, operatorAccount, shift.SHIFT_NO, shiftDayStr, ct);
                    await UpdateCurrentAsync(conn, tx, input.EqmMasterNo, curItem, curValue, operatorAccount, dbNow, ct);
                }
                // ==========================================
                // [WIP 全新修改模式]：如果這次比上次小，差異值無條件等於這次的值
                // ==========================================
                else
                {
                    if (curValue < lastValue)
                    {
                        diffValue = curValue;
                    }
                    else
                    {
                        diffValue = curValue - lastValue;
                    }

                    await InsertHistoryAsync(conn, tx, dbSid, input.EqmMasterNo, curItem, curValue, diffValue, dbNow, operatorAccount, shift.SHIFT_NO, shiftDayStr, ct);
                    await UpdateCurrentAsync(conn, tx, input.EqmMasterNo, curItem, curValue, operatorAccount, dbNow, ct);
                }
            }
            else
            {
                // [第一次上傳] 初次建立不分 WIP/EDC，差異一律給 0
                diffValue = 0;
                await InsertHistoryAsync(conn, tx, dbSid, input.EqmMasterNo, curItem, curValue, diffValue, dbNow, operatorAccount, shift.SHIFT_NO, shiftDayStr, ct);
                await InsertCurrentAsync(conn, tx, dbSid, input.EqmMasterNo, curItem, curValue, operatorAccount, dbNow, ct);
            }

            // 累加絕對值落差，防止 EDC 算出負數溫度時把總差異抵消掉
            batchTotalDiff += Math.Abs(diffValue);
        }

        return batchTotalDiff;
    }

    private async Task<string> GetCurrentEqmStatusInTxAsync(SqlConnection conn, SqlTransaction tx, string eqmNo, CancellationToken ct)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "SELECT TOP (1) [STATUS] FROM [dbo].[EQM_MASTER] WHERE [EQM_MASTER_NO] = @EqmNo;";
        cmd.Parameters.Add(new SqlParameter("@EqmNo", SqlDbType.NVarChar, 255) { Value = eqmNo });
        return (await cmd.ExecuteScalarAsync(ct))?.ToString() ?? string.Empty;
    }

    #region --- 資料庫原生操作輔助 (對齊同事寫法) ---
    private static async Task InsertHistoryAsync(SqlConnection conn, SqlTransaction tx, decimal sid, string eqmNo, string item, decimal output, decimal diff, DateTime now, string user, string shiftNo, string shiftDay, CancellationToken ct)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            INSERT INTO [dbo].[EQM_MASTER_AUTODC_OUTPUT] (
                [EQM_MASTER_AUTODC_OUTPUT_SID], [EQM_MASTER_NO], [AUTODC_ITEM], [AUTODC_OUTPUT], [AUTODC_DIFF_VALUE], 
                [REPORT_TIME], [CREATE_USER], [CREATE_TIME], [EDIT_USER], [EDIT_TIME], [SHIFT_NO], [SHIFT_DAY]
            ) VALUES (
                @Sid, @EqmNo, @Item, @Output, @Diff, @Now, @User, @Now, @User, @Now, @ShiftNo, @ShiftDay
            );
            """;
        cmd.Parameters.Add(new SqlParameter("@Sid", SqlDbType.Decimal) { Value = sid });
        cmd.Parameters.Add(new SqlParameter("@EqmNo", SqlDbType.NVarChar, 255) { Value = eqmNo });
        cmd.Parameters.Add(new SqlParameter("@Item", SqlDbType.NVarChar, 255) { Value = item });
        cmd.Parameters.Add(new SqlParameter("@Output", SqlDbType.Decimal) { Value = output });
        cmd.Parameters.Add(new SqlParameter("@Diff", SqlDbType.Decimal) { Value = diff });
        cmd.Parameters.Add(new SqlParameter("@Now", SqlDbType.DateTime) { Value = now });
        cmd.Parameters.Add(new SqlParameter("@User", SqlDbType.NVarChar, 255) { Value = user });
        cmd.Parameters.Add(new SqlParameter("@ShiftNo", SqlDbType.NVarChar, 255) { Value = shiftNo ?? string.Empty });
        cmd.Parameters.Add(new SqlParameter("@ShiftDay", SqlDbType.NVarChar, 255) { Value = shiftDay });

        if (await cmd.ExecuteNonQueryAsync(ct) != 1)
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.Conflict, $"AutoDC history insert failed: {eqmNo}-{item}");
    }

    private static async Task UpdateCurrentAsync(SqlConnection conn, SqlTransaction tx, string eqmNo, string item, decimal output, string user, DateTime now, CancellationToken ct)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            UPDATE [dbo].[EQM_MASTER_AUTODC_OUTPUT_CUR] 
            SET [AUTODC_OUTPUT] = @Output, [EDIT_USER] = @User, [EDIT_TIME] = @Now 
            WHERE [EQM_MASTER_NO] = @EqmNo AND [AUTODC_ITEM] = @Item;
            """;
        cmd.Parameters.Add(new SqlParameter("@Output", SqlDbType.NVarChar, 255) { Value = output.ToString() });
        cmd.Parameters.Add(new SqlParameter("@User", SqlDbType.NVarChar, 255) { Value = user });
        cmd.Parameters.Add(new SqlParameter("@Now", SqlDbType.DateTime) { Value = now });
        cmd.Parameters.Add(new SqlParameter("@EqmNo", SqlDbType.NVarChar, 255) { Value = eqmNo });
        cmd.Parameters.Add(new SqlParameter("@Item", SqlDbType.NVarChar, 255) { Value = item });

        if (await cmd.ExecuteNonQueryAsync(ct) != 1)
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.Conflict, $"AutoDC current update failed: {eqmNo}-{item}");
    }

    private static async Task InsertCurrentAsync(SqlConnection conn, SqlTransaction tx, decimal sid, string eqmNo, string item, decimal output, string user, DateTime now, CancellationToken ct)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            INSERT INTO [dbo].[EQM_MASTER_AUTODC_OUTPUT_CUR] (
                [EQM_MASTER_AUTODC_OUTPUT_CUR_SID], [EQM_MASTER_NO], [AUTODC_ITEM], [AUTODC_OUTPUT], [CREATE_USER], [CREATE_TIME], [EDIT_USER], [EDIT_TIME]
            ) VALUES (
                @Sid, @EqmNo, @Item, @Output, @User, @Now, @User, @Now
            );
            """;
        cmd.Parameters.Add(new SqlParameter("@Sid", SqlDbType.Decimal) { Value = sid });
        cmd.Parameters.Add(new SqlParameter("@EqmNo", SqlDbType.NVarChar, 255) { Value = eqmNo });
        cmd.Parameters.Add(new SqlParameter("@Item", SqlDbType.NVarChar, 255) { Value = item });
        cmd.Parameters.Add(new SqlParameter("@Output", SqlDbType.NVarChar, 255) { Value = output.ToString() });
        cmd.Parameters.Add(new SqlParameter("@User", SqlDbType.NVarChar, 255) { Value = user });
        cmd.Parameters.Add(new SqlParameter("@Now", SqlDbType.DateTime) { Value = now });

        if (await cmd.ExecuteNonQueryAsync(ct) != 1)
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.Conflict, $"AutoDC current insert failed: {eqmNo}-{item}");
    }

    private async Task<EqmShiftInfoRow> ResolveShiftInfoInTxAsync(SqlConnection conn, SqlTransaction tx, DateTime now, CancellationToken ct)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            SELECT TOP (1) s.[SHIFT_NO], t.[REPORT_DAY]
            FROM [ADM_SHIFT_TIMETABLE] t
            INNER JOIN [ADM_SHIFT] s ON CONVERT(nvarchar(255), s.[ADM_SHIFT_SID]) = t.[SHIFT_SID]
            WHERE t.[START_TIME] < @ShiftTime AND t.[END_TIME] >= @ShiftTime
              AND t.[ENABLE_FLAG] = 'Y' AND s.[ENABLE_FLAG] = 'Y'
            ORDER BY t.[START_TIME];
            """;
        cmd.Parameters.Add(new SqlParameter("@ShiftTime", SqlDbType.NVarChar, 8) { Value = now.ToString("HH:mm:ss") });

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return new EqmShiftInfoRow { SHIFT_NO = string.Empty, REPORT_DAY = "0" };

        return new EqmShiftInfoRow
        {
            SHIFT_NO = reader.GetString(reader.GetOrdinal("SHIFT_NO")),
            REPORT_DAY = reader.GetString(reader.GetOrdinal("REPORT_DAY"))
        };
    }

    private async Task<DateTime> GetDbNowInTxAsync(SqlConnection conn, SqlTransaction tx, CancellationToken ct)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "SELECT SYSDATETIME();";
        return (DateTime)(await cmd.ExecuteScalarAsync(ct) ?? DateTime.Now);
    }
    #endregion
}
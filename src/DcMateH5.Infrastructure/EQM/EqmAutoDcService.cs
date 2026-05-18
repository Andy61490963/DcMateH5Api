using DbExtensions;
using DcMateClassLibrary.Helper;
using DcMateClassLibrary.Helper.HttpHelper;
using DcMateH5.Abstractions.CurrentUser;
using DcMateH5.Abstractions.Eqm.Models;
using DcMateH5.Abstractions.EQM;
using DcMateH5.Abstractions.EQM.Models;
using DcMateH5Api.Models;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Data;

namespace DcMateH5.Infrastructure.EQM;

public class EqmAutoDcService : IEqmAutoDcService
{
    private readonly SQLGenerateHelper _sqlHelper;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IConfiguration _configuration;

    public EqmAutoDcService(
        SQLGenerateHelper sqlHelper,
        ICurrentUserAccessor currentUserAccessor,
        IConfiguration configuration)
    {
        _sqlHelper = sqlHelper;
        _currentUserAccessor = currentUserAccessor;
        _configuration = configuration;
    }

    public static class SystemUsers
    {
        public const string GuiUser = "GUI_USER";
    }

    public async Task<Result<bool>> ProcessAutoDcUploadAsync(EqmAutoDcInputDto input, CancellationToken ct = default)
    {
        await _sqlHelper.TxAsync(
            async (conn, tx, innerCt) =>
            {
                await ProcessAutoDcInTxAsync(conn, tx, input, innerCt);
            },
            isolation: IsolationLevel.ReadCommitted,
            ct: ct);

        return Result<bool>.Ok(true);
    }

    private async Task ProcessAutoDcInTxAsync(
        SqlConnection conn,
        SqlTransaction tx,
        EqmAutoDcInputDto input,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (string.IsNullOrEmpty(input.Value))
            throw new HttpStatusCodeException(System.Net.HttpStatusCode.BadRequest, "VALUE is required.");

        string autoDcWipLimit = _configuration["AppSettings:AUTODC_WIP_LIMIT"] ?? "65535";
        var arrayData = input.Value.Split(',');

        var operatorAccount = _currentUserAccessor.Get()?.Account ?? SystemUsers.GuiUser;
        var dbNow = await GetDbNowInTxAsync(conn, tx, ct);
        string cgiTime = dbNow.ToString("yyyy-MM-dd HH:mm:ss");

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
                // 例如：120 -> 10，差異值就是 -110
                // ==========================================
                if (input.Mode.Equals("EDC", StringComparison.OrdinalIgnoreCase))
                {
                    diffValue = curValue - lastValue;

                    await InsertHistoryAsync(conn, tx, dbSid, input.EqmMasterNo, curItem, curValue, diffValue, dbNow, operatorAccount, shift.SHIFT_NO, shiftDayStr, ct);
                    await UpdateCurrentAsync(conn, tx, input.EqmMasterNo, curItem, curValue, operatorAccount, dbNow, ct);
                }
                // ==========================================
                // [WIP 全新修改模式]：如果這次比上次小，差異值無條件等於這次的值
                // 例如：120 -> 10，差異值就是 10
                // ==========================================
                else
                {
                    if (curValue < lastValue)
                    {
                        // 數值變小了（不管是斷電、清機還是破表歸零），這次的數值就是全新的增量起跳點
                        diffValue = curValue;
                    }
                    else
                    {
                        // 正常穩定遞增
                        diffValue = curValue - lastValue;
                    }

                    // 不管數值是正常遞增，還是變小（包含變為0），一律照實寫入歷史紀錄並更新當前 Current 表
                    await InsertHistoryAsync(conn, tx, dbSid, input.EqmMasterNo, curItem, curValue, diffValue, dbNow, operatorAccount, shift.SHIFT_NO, shiftDayStr, ct);
                    await UpdateCurrentAsync(conn, tx, input.EqmMasterNo, curItem, curValue, operatorAccount, dbNow, ct);
                }
            }
            else
            {
                // [第一次上傳] 初次建立不分 WIP/EDC，差異一律給 0
                await InsertHistoryAsync(conn, tx, dbSid, input.EqmMasterNo, curItem, curValue, 0, dbNow, operatorAccount, shift.SHIFT_NO, shiftDayStr, ct);
                await InsertCurrentAsync(conn, tx, dbSid, input.EqmMasterNo, curItem, curValue, operatorAccount, dbNow, ct);
            }
        }
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
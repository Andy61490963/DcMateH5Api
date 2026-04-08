using System.Data;
using Dapper;
using DcMateClassLibrary.Helper;
using DcMateH5.Abstractions.CurrentUser;
using DcMateH5.Abstractions.KaosuQc;
using DcMateH5.Abstractions.KaosuQc.Models;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace DcMateH5.Infrastructure.KaosuQc;

public class KaosuQcService : IKaosuQcService
{
    private const int SidGenerateRetryLimit = 10;

    private readonly SqlConnection _connection;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly ILogger<KaosuQcService> _logger;

    public KaosuQcService(
        SqlConnection connection,
        ICurrentUserAccessor currentUserAccessor,
        ILogger<KaosuQcService> logger)
    {
        _connection = connection;
        _currentUserAccessor = currentUserAccessor;
        _logger = logger;
    }

    /// <summary>
    /// 批次新增 Kaosu 品檢單頭與單身，使用同一個交易保證全成全敗。
    /// </summary>
    public async Task<KaosuQcBatchCreateResponse> CreateBatchAsync(KaosuQcBatchCreateRequest request, CancellationToken ct = default)
    {
        ValidateRequest(request);

        var inspectionNoList = request.Headers.Select(x => x.InspectionNo.Trim()).ToList();
        var currentUser = _currentUserAccessor.Get();
        var fallbackUser = string.IsNullOrWhiteSpace(currentUser.Account) ? "SYSTEM" : currentUser.Account;

        if (_connection.State != ConnectionState.Open)
        {
            await _connection.OpenAsync(ct);
        }

        await using var tx = (SqlTransaction)await _connection.BeginTransactionAsync(ct);

        try
        {
            var duplicatedNos = await GetExistingInspectionNosAsync(inspectionNoList, tx, ct);
            if (duplicatedNos.Count > 0)
            {
                throw new InvalidOperationException($"INSPECTION_NO 已存在：{string.Join(",", duplicatedNos)}");
            }

            foreach (var header in request.Headers)
            {
                var headerInspectionNo = header.InspectionNo.Trim();
                var headerCreatedUser = ResolveAuditUser(header.CreatedUser, fallbackUser);
                var headerEditUser = ResolveAuditUser(header.EditUser, headerCreatedUser);
                var headerSid = await GenerateUniqueSidAsync("ZZ_KAOSU_INSPECTION_HEADER", tx, ct);

                // 寫入單頭。
                await _connection.ExecuteAsync(new CommandDefinition(
                    commandText: @"
INSERT INTO ZZ_KAOSU_INSPECTION_HEADER
(
    SID,
    INSPECTION_NO,
    INSPECTION_TYPE,
    MATERIAL_CHECK,
    CHECK_RESULT,
    STANDARD_WEIGHT,
    CAVITY,
    MOLD_NO,
    MODL_TOL_NO,
    EQP_NO,
    SOURCE_NO,
    WORK_ORDER,
    ITEM_NO,
    INSPECTION_TIME,
    INSPECTION_RESULT,
    INSPECTOR,
    COMMENT,
    CREATE_USER,
    EDIT_TIME,
    EDIT_USER
)
VALUES
(
    @SID,
    @INSPECTION_NO,
    @INSPECTION_TYPE,
    @MATERIAL_CHECK,
    @CHECK_RESULT,
    @STANDARD_WEIGHT,
    @CAVITY,
    @MOLD_NO,
    @MODL_TOL_NO,
    @EQP_NO,
    @SOURCE_NO,
    @WORK_ORDER,
    @ITEM_NO,
    @INSPECTION_TIME,
    @INSPECTION_RESULT,
    @INSPECTOR,
    @COMMENT,
    @CREATE_USER,
    SYSDATETIME(),
    @EDIT_USER
);",
                    parameters: new
                    {
                        SID = headerSid,
                        INSPECTION_NO = headerInspectionNo,
                        INSPECTION_TYPE = header.InspectionType,
                        MATERIAL_CHECK = header.MaterialCheck,
                        CHECK_RESULT = header.CheckResult,
                        STANDARD_WEIGHT = header.StandardWeight,
                        CAVITY = header.Cavity,
                        MOLD_NO = header.MoldNo,
                        MODL_TOL_NO = header.MoldTolNo,
                        EQP_NO = header.EqpNo,
                        SOURCE_NO = header.SourceNo,
                        WORK_ORDER = header.WorkOrder,
                        ITEM_NO = header.ItemNo,
                        INSPECTION_TIME = header.InspectionTime,
                        INSPECTION_RESULT = header.InspectionResult,
                        INSPECTOR = header.Inspector,
                        COMMENT = header.Comment,
                        CREATE_USER = headerCreatedUser,
                        EDIT_USER = headerEditUser
                    },
                    transaction: tx,
                    cancellationToken: ct));

                foreach (var detail in header.Details)
                {
                    var detailCreatedUser = ResolveAuditUser(detail.CreatedUser, headerCreatedUser);
                    var detailEditUser = ResolveAuditUser(detail.EditUser, headerEditUser);
                    var detailSid = await GenerateUniqueSidAsync("ZZ_KAOSU_INSPECTION_DETAIL", tx, ct);

                    // 單身 INSPECTION_NO 強制覆寫為單頭 INSPECTION_NO，避免前端送錯。
                    await _connection.ExecuteAsync(new CommandDefinition(
                        commandText: @"
INSERT INTO ZZ_KAOSU_INSPECTION_DETAIL
(
    SID,
    HEADER_SID,
    INSPECTION_NO,
    ITEM_NO,
    INSPECTION_ITEM,
    INSPECTION_VALUE,
    INSPECTION_TIME_MINUTES,
    USL,
    UCL,
    SAMPLE_SIZE,
    TARGET,
    LCL,
    LSL,
    BASE_WORK_TIME,
    ROW_COUNT,
    CREATE_USER,
    EDIT_TIME,
    EDIT_USER
)
VALUES
(
    @SID,
    @HEADER_SID,
    @INSPECTION_NO,
    @ITEM_NO,
    @INSPECTION_ITEM,
    @INSPECTION_VALUE,
    @INSPECTION_TIME_MINUTES,
    @USL,
    @UCL,
    @SAMPLE_SIZE,
    @TARGET,
    @LCL,
    @LSL,
    @BASE_WORK_TIME,
    @ROW_COUNT,
    @CREATE_USER,
    SYSDATETIME(),
    @EDIT_USER
);",
                        parameters: new
                        {
                            SID = detailSid,
                            HEADER_SID = headerSid,
                            INSPECTION_NO = headerInspectionNo,
                            ITEM_NO = detail.ItemNo,
                            INSPECTION_ITEM = detail.InspectionItem,
                            INSPECTION_VALUE = detail.InspectionValue,
                            INSPECTION_TIME_MINUTES = detail.InspectionTimeMinutes,
                            USL = detail.Usl,
                            UCL = detail.Ucl,
                            SAMPLE_SIZE = detail.SampleSize, 
                            TARGET = detail.Target,
                            LCL = detail.Lcl,
                            LSL = detail.Lsl,
                            BASE_WORK_TIME = detail.BaseWorkTime,
                            ROW_COUNT = detail.RowCount,
                            CREATE_USER = detailCreatedUser,
                            EDIT_USER = detailEditUser
                        },
                        transaction: tx,
                        cancellationToken: ct));
                }
            }

            await tx.CommitAsync(ct);

            return new KaosuQcBatchCreateResponse
            {
                InspectionNos = inspectionNoList
            };
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    /// <summary>
    /// 驗證請求資料完整性，避免不必要 DB 往返。
    /// </summary>
    private static void ValidateRequest(KaosuQcBatchCreateRequest request)
    {
        if (request.Headers is null || request.Headers.Count == 0)
        {
            throw new ArgumentException("Headers 不可為空。", nameof(request));
        }

        var duplicatedRequestNos = request.Headers
            .Select(x => x.InspectionNo?.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .GroupBy(x => x!, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicatedRequestNos.Count > 0)
        {
            throw new ArgumentException($"請求中包含重複 INSPECTION_NO：{string.Join(",", duplicatedRequestNos)}", nameof(request));
        }

        var invalidHeader = request.Headers.FirstOrDefault(x => string.IsNullOrWhiteSpace(x.InspectionNo));
        if (invalidHeader is not null)
        {
            throw new ArgumentException("INSPECTION_NO 不可為空。", nameof(request));
        }
    }

    /// <summary>
    /// 取得資料庫中已存在的檢驗單號。
    /// </summary>
    private async Task<List<string>> GetExistingInspectionNosAsync(IEnumerable<string> inspectionNos, SqlTransaction tx, CancellationToken ct)
    {
        var sql = @"
SELECT INSPECTION_NO
FROM ZZ_KAOSU_INSPECTION_HEADER
WHERE INSPECTION_NO IN @InspectionNos;";

        var rows = await _connection.QueryAsync<string>(new CommandDefinition(
            commandText: sql,
            parameters: new { InspectionNos = inspectionNos },
            transaction: tx,
            cancellationToken: ct));

        return rows.Select(x => x.Trim()).ToList();
    }

    /// <summary>
    /// 以既有工具產生 SID，並檢查資料表唯一性，避免極端碰撞造成主鍵衝突。
    /// </summary>
    private async Task<decimal> GenerateUniqueSidAsync(string tableName, SqlTransaction tx, CancellationToken ct)
    {
        var sql = tableName switch
        {
            "ZZ_KAOSU_INSPECTION_HEADER" => "SELECT COUNT(1) FROM ZZ_KAOSU_INSPECTION_HEADER WHERE SID = @SID;",
            "ZZ_KAOSU_INSPECTION_DETAIL" => "SELECT COUNT(1) FROM ZZ_KAOSU_INSPECTION_DETAIL WHERE SID = @SID;",
            _ => throw new ArgumentOutOfRangeException(nameof(tableName), tableName, "不支援的 SID 檢查資料表。")
        };

        for (var i = 0; i < SidGenerateRetryLimit; i++)
        {
            var sid = RandomHelper.GenerateRandomDecimal();
            var exists = await _connection.ExecuteScalarAsync<int>(new CommandDefinition(
                commandText: sql,
                parameters: new { SID = sid },
                transaction: tx,
                cancellationToken: ct));

            if (exists == 0)
            {
                return sid;
            }
        }

        _logger.LogError("產生 SID 連續碰撞，tableName={TableName}", tableName);
        throw new InvalidOperationException("系統忙碌中，請稍後再試。");
    }

    /// <summary>
    /// 決定審計欄位帳號，優先使用 request 值，其次回退使用登入者。
    /// </summary>
    private static string ResolveAuditUser(string? requestUser, string fallbackUser)
    {
        return string.IsNullOrWhiteSpace(requestUser) ? fallbackUser : requestUser.Trim();
    }
}

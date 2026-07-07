using System.Data;
using Dapper;
using DcMateClassLibrary.Helper;
using DcMateH5.Abstractions.CurrentUser;
using DcMateH5.Abstractions.Qc;
using DcMateH5.Abstractions.Qc.Models;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace DcMateH5.Infrastructure.Qc;

public class QcService : IQcService
{
    private const int SidGenerateRetryLimit = 10;
    private const string HeaderTable = "QMM_INSPECTION_HEADER";
    private const string DetailTable = "QMM_INSPECTION_DETAIL";

    private readonly SqlConnection _connection;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly ILogger<QcService> _logger;

    public QcService(SqlConnection connection, ICurrentUserAccessor currentUserAccessor, ILogger<QcService> logger)
    {
        _connection = connection;
        _currentUserAccessor = currentUserAccessor;
        _logger = logger;
    }

    public async Task<QcBatchCreateResponse> CreateBatchAsync(QcBatchCreateRequest request, CancellationToken ct = default)
    {
        ValidateRequest(request);

        var inspectionNos = request.HEADERS.Select(x => x.INSPECTION_NO.Trim()).ToList();
        var currentUser = _currentUserAccessor.Get();
        var fallbackUser = string.IsNullOrWhiteSpace(currentUser.Account) ? "SYSTEM" : currentUser.Account;

        if (_connection.State != ConnectionState.Open)
        {
            await _connection.OpenAsync(ct);
        }

        await using var tx = (SqlTransaction)await _connection.BeginTransactionAsync(ct);
        try
        {
            var duplicatedNos = await GetExistingInspectionNosAsync(inspectionNos, tx, ct);
            if (duplicatedNos.Count > 0)
            {
                throw new InvalidOperationException($"INSPECTION_NO already exists: {string.Join(",", duplicatedNos)}");
            }

            foreach (var header in request.HEADERS)
            {
                var inspectionNo = header.INSPECTION_NO.Trim();
                var createdUser = ResolveAuditUser(header.CREATE_USER, fallbackUser);
                var editUser = ResolveAuditUser(header.EDIT_USER, createdUser);
                var headerSid = await GenerateUniqueSidAsync(HeaderTable, tx, ct);

                await _connection.ExecuteAsync(new CommandDefinition(
                    """
                    INSERT INTO QMM_INSPECTION_HEADER
                    (
                        QMM_INSPECTION_HEADER_SID, INSPECTION_NO, INSPECTION_TYPE,
                        MATERIAL_CHECK, CHECK_RESULT, STANDARD_WEIGHT, SAMPLING_QTY, CAVITY, MOLD_NO,
                        MOLD_TOL_NO, EQP_NO, WIP_OPI_WDOEACICO_HIST_SID, SOURCE_NO,
                        LOT,
                        WORK_ORDER, ITEM_NO, INSPECTION_TIME, INSPECTION_RESULT, INSPECTOR,
                        APPROVE_USER, PRODUCTION_STATUS, COMMENT, CREATE_USER, EDIT_TIME, EDIT_USER
                    )
                    VALUES
                    (
                        @SID, @INSPECTION_NO, @INSPECTION_TYPE,
                        @MATERIAL_CHECK, @CHECK_RESULT, @STANDARD_WEIGHT, @SAMPLING_QTY, @CAVITY, @MOLD_NO,
                        @MOLD_TOL_NO, @EQP_NO, @WIP_OPI_WDOEACICO_HIST_SID, @SOURCE_NO,
                        @LOT,
                        @WORK_ORDER, @ITEM_NO, @INSPECTION_TIME, @INSPECTION_RESULT, @INSPECTOR,
                        @APPROVE_USER, @PRODUCTION_STATUS, @COMMENT, @CREATE_USER, SYSDATETIME(), @EDIT_USER
                    );
                    """,
                    new
                    {
                        SID = headerSid,
                        INSPECTION_NO = inspectionNo,
                        INSPECTION_TYPE = header.INSPECTION_TYPE,
                        MATERIAL_CHECK = header.MATERIAL_CHECK,
                        CHECK_RESULT = header.CHECK_RESULT,
                        STANDARD_WEIGHT = header.STANDARD_WEIGHT,
                        SAMPLING_QTY = header.SAMPLING_QTY,
                        CAVITY = header.CAVITY,
                        MOLD_NO = header.MOLD_NO,
                        MOLD_TOL_NO = header.MOLD_TOL_NO,
                        EQP_NO = header.EQP_NO,
                        WIP_OPI_WDOEACICO_HIST_SID = header.WIP_OPI_WDOEACICO_HIST_SID,
                        SOURCE_NO = header.SOURCE_NO,
                        LOT = header.LOT,
                        WORK_ORDER = header.WORK_ORDER,
                        ITEM_NO = header.ITEM_NO,
                        INSPECTION_TIME = header.INSPECTION_TIME,
                        INSPECTION_RESULT = header.INSPECTION_RESULT,
                        INSPECTOR = header.INSPECTOR,
                        APPROVE_USER = header.APPROVE_USER,
                        PRODUCTION_STATUS = header.PRODUCTION_STATUS,
                        COMMENT = header.COMMENT,
                        CREATE_USER = createdUser,
                        EDIT_USER = editUser
                    },
                    tx,
                    cancellationToken: ct));

                foreach (var detail in header.DETAILS)
                {
                    var detailSid = await GenerateUniqueSidAsync(DetailTable, tx, ct);
                    await _connection.ExecuteAsync(new CommandDefinition(
                        """
                        INSERT INTO QMM_INSPECTION_DETAIL
                        (
                            QMM_INSPECTION_DETAIL_SID, QMM_INSPECTION_HEADER_SID, INSPECTION_NO,
                            ITEM_NO, INSPECTION_ITEM, INSPECTION_VALUE, INSPECTION_DETAIL_RESULT,
                            INSPECTION_TIME_MINUTES,
                            USL, UCL, SAMPLE_SIZE, TARGET, LCL, LSL, BASE_WORK_TIME, ROW_COUNT,
                            CREATE_USER, EDIT_TIME, EDIT_USER
                        )
                        VALUES
                        (
                            @SID, @HEADER_SID, @INSPECTION_NO,
                            @ITEM_NO, @INSPECTION_ITEM, @INSPECTION_VALUE, @INSPECTION_DETAIL_RESULT,
                            @INSPECTION_TIME_MINUTES,
                            @USL, @UCL, @SAMPLE_SIZE, @TARGET, @LCL, @LSL, @BASE_WORK_TIME, @ROW_COUNT,
                            @CREATE_USER, SYSDATETIME(), @EDIT_USER
                        );
                        """,
                        new
                        {
                            SID = detailSid,
                            HEADER_SID = headerSid,
                            INSPECTION_NO = inspectionNo,
                            ITEM_NO = detail.ITEM_NO,
                            INSPECTION_ITEM = detail.INSPECTION_ITEM,
                            INSPECTION_VALUE = detail.INSPECTION_VALUE,
                            INSPECTION_DETAIL_RESULT = detail.INSPECTION_DETAIL_RESULT,
                            INSPECTION_TIME_MINUTES = detail.INSPECTION_TIME_MINUTES,
                            USL = detail.USL,
                            UCL = detail.UCL,
                            SAMPLE_SIZE = detail.SAMPLE_SIZE,
                            TARGET = detail.TARGET,
                            LCL = detail.LCL,
                            LSL = detail.LSL,
                            BASE_WORK_TIME = detail.BASE_WORK_TIME,
                            ROW_COUNT = detail.ROW_COUNT,
                            CREATE_USER = ResolveAuditUser(detail.CREATE_USER, createdUser),
                            EDIT_USER = ResolveAuditUser(detail.EDIT_USER, editUser)
                        },
                        tx,
                        cancellationToken: ct));
                }
            }

            await tx.CommitAsync(ct);
            return new QcBatchCreateResponse { INSPECTION_NOS = inspectionNos };
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    private static void ValidateRequest(QcBatchCreateRequest request)
    {
        if (request.HEADERS is null || request.HEADERS.Count == 0)
        {
            throw new ArgumentException("Headers cannot be empty.", nameof(request));
        }

        var duplicatedNos = request.HEADERS
            .Select(x => x.INSPECTION_NO?.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .GroupBy(x => x!, StringComparer.OrdinalIgnoreCase)
            .Where(x => x.Count() > 1)
            .Select(x => x.Key)
            .ToList();

        if (duplicatedNos.Count > 0)
        {
            throw new ArgumentException($"Request contains duplicate INSPECTION_NO: {string.Join(",", duplicatedNos)}", nameof(request));
        }

        if (request.HEADERS.Any(x => string.IsNullOrWhiteSpace(x.INSPECTION_NO)))
        {
            throw new ArgumentException("INSPECTION_NO cannot be empty.", nameof(request));
        }

        foreach (var header in request.HEADERS)
        {
            ValidateRequired(header.INSPECTION_TYPE, nameof(header.INSPECTION_TYPE));
            ValidateRequired(header.WORK_ORDER, nameof(header.WORK_ORDER));
            ValidateRequired(header.ITEM_NO, nameof(header.ITEM_NO));
            ValidateRequired(header.INSPECTION_RESULT, nameof(header.INSPECTION_RESULT));
            ValidateRequired(header.INSPECTOR, nameof(header.INSPECTOR));

            if (header.INSPECTION_TIME == default)
            {
                throw new ArgumentException("INSPECTION_TIME is required.", nameof(request));
            }

            foreach (var detail in header.DETAILS)
            {
                ValidateRequired(detail.ITEM_NO, nameof(detail.ITEM_NO));
                ValidateRequired(detail.INSPECTION_ITEM, nameof(detail.INSPECTION_ITEM));
            }
        }
    }

    private static void ValidateRequired(string value, string columnName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{columnName} cannot be empty.");
        }
    }

    private async Task<List<string>> GetExistingInspectionNosAsync(IEnumerable<string> inspectionNos, SqlTransaction tx, CancellationToken ct)
    {
        var rows = await _connection.QueryAsync<string>(new CommandDefinition(
            "SELECT INSPECTION_NO FROM QMM_INSPECTION_HEADER WHERE INSPECTION_NO IN @InspectionNos;",
            new { InspectionNos = inspectionNos },
            tx,
            cancellationToken: ct));

        return rows.Select(x => x.Trim()).ToList();
    }

    private async Task<decimal> GenerateUniqueSidAsync(string tableName, SqlTransaction tx, CancellationToken ct)
    {
        var sql = tableName switch
        {
            HeaderTable => "SELECT COUNT(1) FROM QMM_INSPECTION_HEADER WHERE QMM_INSPECTION_HEADER_SID = @SID;",
            DetailTable => "SELECT COUNT(1) FROM QMM_INSPECTION_DETAIL WHERE QMM_INSPECTION_DETAIL_SID = @SID;",
            _ => throw new ArgumentOutOfRangeException(nameof(tableName), tableName, "Unsupported SID table.")
        };

        for (var i = 0; i < SidGenerateRetryLimit; i++)
        {
            var sid = RandomHelper.GenerateRandomDecimal();
            var exists = await _connection.ExecuteScalarAsync<int>(new CommandDefinition(sql, new { SID = sid }, tx, cancellationToken: ct));
            if (exists == 0)
            {
                return sid;
            }
        }

        _logger.LogError("Failed to generate unique SID for {TableName}", tableName);
        throw new InvalidOperationException("Unable to generate a unique SID. Try again later.");
    }

    private static string ResolveAuditUser(string? requestUser, string fallbackUser)
    {
        return string.IsNullOrWhiteSpace(requestUser) ? fallbackUser : requestUser.Trim();
    }
}

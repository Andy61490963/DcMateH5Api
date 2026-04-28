using System.Security.Claims;
using System.Text.Json;
using Dapper;
using DbExtensions;
using DbExtensions.DbExecutor.Service;
using DcMateClassLibrary.Models;
using DcMateH5.Abstractions.CurrentUser;
using DcMateH5.Abstractions.Log;
using DcMateH5.Abstractions.Log.Models;
using DcMateH5.Infrastructure.Wip;
using DcMateH5Api.Areas.Wip.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Xunit;

namespace DcMateH5ApiTest.Wip;

[Collection(DatabaseIntegrationCollection.Name)]
public class LotCheckInAsyncIntegrationTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task LotCheckInAsync_ShouldInsertAndCleanupLotCheckInData()
    {
        var connectionString = TestConfiguration.LoadConnectionString();
        var arrangement = await LoadArrangementAsync(connectionString);
        var lotCode = $"ITEST-CI-{DateTime.UtcNow:yyyyMMddHHmmssfff}";
        const decimal lotQty = 2m;
        const decimal dataLinkSid = 900000000101m;

        var service = CreateService(connectionString, arrangement.AccountNo);

        try
        {
            var createLotResult = await service.CreateLotAsync(
                new WipCreateLotInputDto
                {
                    DATA_LINK_SID = dataLinkSid,
                    LOT = lotCode,
                    ALIAS_LOT1 = $"{lotCode}-A1",
                    ALIAS_LOT2 = $"{lotCode}-A2",
                    WO = arrangement.WorkOrder,
                    ROUTE_SID = arrangement.RouteSid,
                    LOT_QTY = lotQty,
                    REPORT_TIME = DateTime.Now,
                    ACCOUNT_NO = arrangement.AccountNo,
                    INPUT_FORM_NAME = "DcMateH5ApiTest",
                    COMMENT = "LotCheckInAsync integration test"
                });
            Assert.True(createLotResult.IsSuccess);

            var result = await service.LotCheckInAsync(
                new WipLotCheckInInputDto
                {
                    LOT = lotCode,
                    DATA_LINK_SID = dataLinkSid + 1,
                    ACCOUNT_NO = arrangement.AccountNo,
                    EQP_NO = arrangement.EquipmentNo,
                    SHIFT_SID = arrangement.ShiftSid,
                    LOT_SUB_STATUS_CODE = "NORMAL",
                    COMMENT = "LotCheckInAsync integration test",
                    INPUT_FORM_NAME = "DcMateH5ApiTest"
                });

            Assert.True(result.IsSuccess);
            Assert.True(result.Data);

            await using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync();

            var lot = await conn.QuerySingleOrDefaultAsync<CheckedInLotRow>(
                """
                SELECT LOT, LOT_STATUS_CODE, CUR_OPER_BATCH_ID, CUR_OPER_FIRST_IN_FLAG, LOT_SUB_STATUS_CODE
                FROM WIP_LOT
                WHERE LOT = @Lot
                """,
                new { Lot = lotCode });

            Assert.NotNull(lot);
            Assert.Equal("Run", lot!.LOT_STATUS_CODE);
            Assert.Equal(dataLinkSid + 1, lot.CUR_OPER_BATCH_ID);
            Assert.Equal("Y", lot.CUR_OPER_FIRST_IN_FLAG);
            Assert.Equal("NORMAL", lot.LOT_SUB_STATUS_CODE);

            var checkInHist = await conn.QuerySingleOrDefaultAsync<CheckInHistRow>(
                """
                SELECT TOP (1) ACTION_CODE, CONTROL_MODE, TOTAL_USER_COUNT, LOT_SUB_STATUS_CODE
                FROM WIP_LOT_HIST
                WHERE LOT = @Lot AND ACTION_CODE = 'CHECK_IN'
                ORDER BY SEQ DESC
                """,
                new { Lot = lotCode });

            Assert.NotNull(checkInHist);
            Assert.Equal("CHECK_IN", checkInHist!.ACTION_CODE);
            Assert.Equal("ONE", checkInHist.CONTROL_MODE);
            Assert.Equal(1, checkInHist.TOTAL_USER_COUNT);
            Assert.Equal("NORMAL", checkInHist.LOT_SUB_STATUS_CODE);

            var currentUserCount = await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM WIP_LOT_CUR_USER WHERE LOT = @Lot AND CREATE_USER = @AccountNo",
                new { Lot = lotCode, AccountNo = arrangement.AccountNo });

            Assert.Equal(1, currentUserCount);

            var userHistCount = await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM WIP_LOT_USER_HIST WHERE CREATE_USER = @AccountNo AND IN_WIP_LOT_HIST_SID IN (SELECT WIP_LOT_HIST_SID FROM WIP_LOT_HIST WHERE LOT = @Lot AND ACTION_CODE = 'CHECK_IN')",
                new { Lot = lotCode, AccountNo = arrangement.AccountNo });

            Assert.Equal(1, userHistCount);

            var eqpHistCount = await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM WIP_LOT_EQP_HIST WHERE EQP_NO = @EqpNo AND WIP_LOT_HIST_SID IN (SELECT WIP_LOT_HIST_SID FROM WIP_LOT_HIST WHERE LOT = @Lot AND ACTION_CODE = 'CHECK_IN')",
                new { Lot = lotCode, EqpNo = arrangement.EquipmentNo });

            Assert.Equal(1, eqpHistCount);
        }
        finally
        {
            await CleanupAsync(connectionString, arrangement.WorkOrder, arrangement.PreviousReleaseQty, lotCode);
        }
    }

    private static LotBaseSettingService CreateService(string connectionString, string accountNo)
    {
        var options = Options.Create(new DbOptions { Connection = connectionString });
        var connectionFactory = new SqlConnectionFactory(options);
        var connection = connectionFactory.Create();
        var dbExecutor = new DbExecutor(
            connection,
            new DbTransactionContext(),
            new FakeLogService(),
            new HttpContextAccessor());

        var sqlHelper = new SQLGenerateHelper(dbExecutor, new FakeCurrentUserAccessor(accountNo));

        return new LotBaseSettingService(
            sqlHelper,
            new SelectDtoService(sqlHelper));
    }

    private static async Task<TestArrangement> LoadArrangementAsync(string connectionString)
    {
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync();

        var row = await conn.QuerySingleOrDefaultAsync<TestArrangement>(
            """
            SELECT TOP (1)
                u.ACCOUNT_NO AS AccountNo,
                u.SHIFT_SID AS ShiftSid,
                w.WO AS WorkOrder,
                w.PART_NO AS PartNo,
                r.WIP_ROUTE_SID AS RouteSid,
                w.RELEASE_QTY AS PreviousReleaseQty,
                e.EQM_MASTER_NO AS EquipmentNo
            FROM WIP_WO w
            INNER JOIN WIP_PARTNO p ON p.WIP_PARTNO_NO = w.PART_NO
            INNER JOIN WIP_ROUTE r ON r.WIP_ROUTE_NO = w.ROUTE_NO OR r.WIP_ROUTE_NAME = w.ROUTE_NO
            CROSS JOIN (
                SELECT TOP (1) ACCOUNT_NO, CAST(NULL AS decimal(18, 0)) AS SHIFT_SID
                FROM ADM_USER
                WHERE ACCOUNT_NO IS NOT NULL
                  AND [TYPE] = 'UMM_USER'
                ORDER BY USER_SID DESC
            ) u
            CROSS JOIN (
                SELECT TOP (1) EQM_MASTER_NO
                FROM EQM_MASTER
                WHERE EQM_MASTER_NO IS NOT NULL
                ORDER BY EQM_MASTER_SID
            ) e
            WHERE EXISTS (
                SELECT 1
                FROM WIP_ROUTE_OPERATION ro
                WHERE ro.WIP_ROUTE_SID = r.WIP_ROUTE_SID
            )
            ORDER BY w.WO_SID DESC
            """);

        if (row == null)
            throw new InvalidOperationException("No test arrangement could be resolved from the database.");

        return row;
    }

    private static async Task CleanupAsync(string connectionString, string workOrder, decimal? previousReleaseQty, string lotCode)
    {
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();

        await conn.ExecuteAsync(
            """
            DELETE FROM WIP_LOT_EQP_HIST
            WHERE WIP_LOT_HIST_SID IN (SELECT WIP_LOT_HIST_SID FROM WIP_LOT_HIST WHERE LOT = @Lot)
            """,
            new { Lot = lotCode },
            tx);

        await conn.ExecuteAsync(
            "DELETE FROM WIP_LOT_CUR_EQP WHERE LOT = @Lot",
            new { Lot = lotCode },
            tx);

        await conn.ExecuteAsync(
            """
            DELETE FROM WIP_LOT_USER_HIST
            WHERE IN_WIP_LOT_HIST_SID IN (SELECT WIP_LOT_HIST_SID FROM WIP_LOT_HIST WHERE LOT = @Lot)
            """,
            new { Lot = lotCode },
            tx);

        await conn.ExecuteAsync(
            "DELETE FROM WIP_LOT_CUR_USER WHERE LOT = @Lot",
            new { Lot = lotCode },
            tx);

        await conn.ExecuteAsync(
            "DELETE FROM WIP_LOT_HIST WHERE LOT = @Lot",
            new { Lot = lotCode },
            tx);

        await conn.ExecuteAsync(
            "DELETE FROM WIP_LOT WHERE LOT = @Lot",
            new { Lot = lotCode },
            tx);

        await conn.ExecuteAsync(
            "UPDATE WIP_WO SET RELEASE_QTY = @ReleaseQty WHERE WO = @Wo",
            new { ReleaseQty = previousReleaseQty, Wo = workOrder },
            tx);

        await tx.CommitAsync();
    }

    private sealed class TestArrangement
    {
        public string AccountNo { get; init; } = null!;
        public decimal? ShiftSid { get; init; }
        public string WorkOrder { get; init; } = null!;
        public string PartNo { get; init; } = null!;
        public decimal RouteSid { get; init; }
        public decimal? PreviousReleaseQty { get; init; }
        public string EquipmentNo { get; init; } = null!;
    }

    private sealed class CheckedInLotRow
    {
        public string LOT { get; init; } = null!;
        public string LOT_STATUS_CODE { get; init; } = null!;
        public decimal CUR_OPER_BATCH_ID { get; init; }
        public string CUR_OPER_FIRST_IN_FLAG { get; init; } = null!;
        public string? LOT_SUB_STATUS_CODE { get; init; }
    }

    private sealed class CheckInHistRow
    {
        public string ACTION_CODE { get; init; } = null!;
        public string CONTROL_MODE { get; init; } = null!;
        public decimal TOTAL_USER_COUNT { get; init; }
        public string? LOT_SUB_STATUS_CODE { get; init; }
    }

    private sealed class FakeCurrentUserAccessor(string accountNo) : ICurrentUserAccessor
    {
        public CurrentUserSnapshot Get()
        {
            var claims = new[]
            {
                new Claim(DcMateClassLibrary.Models.AppClaimTypes.Account, accountNo),
                new Claim(DcMateClassLibrary.Models.AppClaimTypes.UserId, Guid.NewGuid().ToString()),
                new Claim(DcMateClassLibrary.Models.AppClaimTypes.UserLv, "1")
            };

            return CurrentUserSnapshot.From(new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")));
        }
    }

    private sealed class FakeLogService : ILogService
    {
        public Task LogAsync(SqlLogEntry entry, CancellationToken ct = default) => Task.CompletedTask;

        public Task<IReadOnlyList<SqlLogEntry>> GetLogsAsync(SqlLogQuery query, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<SqlLogEntry>>(Array.Empty<SqlLogEntry>());
    }

    private static class TestConfiguration
    {
        public static string LoadConnectionString()
        {
            var root = FindSolutionRoot();
            var json = File.ReadAllText(Path.Combine(root, "src", "DcMateH5Api", "appsettings.json"));
            using var doc = JsonDocument.Parse(json);

            return doc.RootElement
                .GetProperty("ConnectionStrings")
                .GetProperty("Connection")
                .GetString()
                   ?? throw new InvalidOperationException("Connection string not found.");
        }

        private static string FindSolutionRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "DcMateH5Api.sln")))
                {
                    return dir.FullName;
                }

                dir = dir.Parent;
            }

            throw new DirectoryNotFoundException("Could not locate solution root.");
        }
    }
}
